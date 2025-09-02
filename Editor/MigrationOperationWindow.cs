using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Migrator;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MigratorEditor
{
    public class MigrationOperationWindow : EditorWindow
    {
        [SerializeField] TreeViewState<int> _treeViewState;

        private static Type[] _typeCache;
        private static Type[] _strategyCache;

        private List<UpgradablePrefab> _upgradablePrefabs;
        private List<UpgradableScene> _upgradableScenes;
        private List<UpgradableScriptableObject> _upgradableScriptableObjects;
        private MigrationOperationTreeView _treeView;
        private Vector2 _scrollPosition;

        [MenuItem("Tools/Migration Operation")]
        public static void ShowWindow() => GetWindow(typeof(MigrationOperationWindow), false, "Migration Operation", true);

        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        private void OnEnable()
        {
            var allTypes = AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(a => !a.FullName.StartsWith("JetBrains")) // bug with the Rider package
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsNestedPrivate);

            _typeCache = allTypes
                .Where(t => t
                    .GetFields()
                    .Any(f => f.CustomAttributes
                        .Any(a => a.AttributeType == typeof(MigrateFieldAttribute))
                    )
                )
                .ToArray();

            _strategyCache = allTypes
                .Where(y => y.GetInterfaces().Contains(typeof(IMigrationOperationStrategy)))
                .ToArray();
        }

        private void OnGUI()
        {
            if (GUI.Button(new Rect(0, 0, 100, 40), "Refresh"))
                RefreshList();

            if (GUI.Button(new Rect(120, 0, 100, 40), "Migrate"))
                Migrate();

            using var scroll = new EditorGUILayout.ScrollViewScope(_scrollPosition);
            _treeView?.OnGUI(new Rect(0, 60, position.width, position.height - 80));
        }

        private void RefreshList()
        {
            _upgradablePrefabs = new();
            _upgradableScenes = new();
            _upgradableScriptableObjects = new();

            // Prefabs
            var prefabPaths = AssetDatabase.FindAssets("t:Prefab").Select(AssetDatabase.GUIDToAssetPath);

            foreach (var prefabPath in prefabPaths)
            {
                var prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var prefabType = PrefabUtility.GetPrefabAssetType(prefabObject);

                if (prefabType is not PrefabAssetType.Regular and not PrefabAssetType.Variant)
                    continue;

                var prefab = new UpgradablePrefab
                {
                    path = prefabPath,
                    isPrefabVariant = prefabType == PrefabAssetType.Variant,
                    components = new()
                };

                foreach (var type in _typeCache)
                {
                    if (!TypeIsComponent(type))
                        continue;

                    ProcessComponents(prefabObject.GetComponentsInChildren(type), prefab.components);
                }

                if (prefab.components.Count != 0)
                    _upgradablePrefabs.Add(prefab);
            }

            // Scenes
            var currentScene = SceneManager.GetActiveScene().path;
            var sceneCount = SceneManager.sceneCountInBuildSettings;

            for (var i = 0; i < sceneCount; ++i)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(i);

                var scene = new UpgradableScene
                {
                    path = scenePath,
                    components = new()
                };

                EditorSceneManager.OpenScene(scenePath);

                foreach (var type in _typeCache)
                {
                    if (!TypeIsComponent(type))
                        continue;

                    ProcessComponents(FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Component>(), scene.components);
                }

                if (scene.components.Count != 0)
                    _upgradableScenes.Add(scene);
            }

            // Scriptable Objects
            foreach (var type in _typeCache)
            {
                if (!TypeIsScriptableObject(type))
                    continue;

                var scriptableObjectPaths = AssetDatabase.FindAssets("t:" + type.Name).Select(AssetDatabase.GUIDToAssetPath);

                foreach (var scriptableObjectPath in scriptableObjectPaths)
                {
                    var scriptableObject = AssetDatabase.LoadAssetAtPath(scriptableObjectPath, type);

                    var upgradableScriptableObject = new UpgradableScriptableObject
                    {
                        path = scriptableObjectPath,
                        type = type,
                        upgradableFields = new()
                    };

                    using var serializedObject = new SerializedObject(scriptableObject);

                    ProcessFields(serializedObject, upgradableScriptableObject.type, upgradableScriptableObject.upgradableFields);

                    if (upgradableScriptableObject.upgradableFields.Count == 0)
                        continue;

                    _upgradableScriptableObjects.Add(upgradableScriptableObject);
                }
            }

            EditorSceneManager.OpenScene(currentScene);

            _treeViewState ??= new TreeViewState<int>();
            _treeView = new MigrationOperationTreeView(_treeViewState, _upgradableScenes, _upgradablePrefabs, _upgradableScriptableObjects);
        }

        private void Migrate()
        {
            // Prefabs
            foreach (var upgradablePrefab in _upgradablePrefabs)
            {
                var prefab = PrefabUtility.LoadPrefabContents(upgradablePrefab.path);

                foreach (var upgradableComponent in upgradablePrefab.components)
                {
                    var objectOwner = GetChildFromPath(prefab.transform, upgradableComponent.owner);
                    var actualComponent = objectOwner.GetComponent(upgradableComponent.type);

                    using var serializedObject = new SerializedObject(actualComponent);

                    serializedObject.Update();

                    foreach (var upgradableField in upgradableComponent.upgradableFields)
                    {
                        var originalProperty = serializedObject.FindProperty(upgradableField.originalField);
                        var targetProperty = serializedObject.FindProperty(upgradableField.targetField);

                        upgradableField.strategy.Migrate(originalProperty, targetProperty, upgradableField);
                    }

                    serializedObject.ApplyModifiedPropertiesWithoutUndo();

                    PrefabUtility.RecordPrefabInstancePropertyModifications(actualComponent);
                }

                PrefabUtility.SaveAsPrefabAsset(prefab, upgradablePrefab.path);
                PrefabUtility.UnloadPrefabContents(prefab);
            }

            // Scriptable Objects
            foreach (var upgradableScriptableObject in _upgradableScriptableObjects)
            {
                var scriptableObject = (ScriptableObject)AssetDatabase.LoadAssetAtPath(upgradableScriptableObject.path, upgradableScriptableObject.type);

                using var serializedObject = new SerializedObject(scriptableObject);

                serializedObject.Update();

                foreach (var upgradableField in upgradableScriptableObject.upgradableFields)
                {
                    var originalProperty = serializedObject.FindProperty(upgradableField.originalField);
                    var targetProperty = serializedObject.FindProperty(upgradableField.targetField);

                    upgradableField.strategy.Migrate(originalProperty, targetProperty, upgradableField);
                }

                serializedObject.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(scriptableObject);
            }

            AssetDatabase.SaveAssets();

            // Scenes
            var currentScene = SceneManager.GetActiveScene().path;

            foreach (var upgradableScene in _upgradableScenes)
            {
                var scene = EditorSceneManager.OpenScene(upgradableScene.path);
                var topLevelObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                foreach (var upgradableComponent in upgradableScene.components)
                {
                    var firstEntryIndex = upgradableComponent.owner.IndexOf("/", StringComparison.Ordinal);

                    Transform objectOwner;

                    if (firstEntryIndex < 0)
                        objectOwner = topLevelObjects.First(x => x.name == upgradableComponent.owner).transform;
                    else
                        objectOwner = GetChildFromPath(topLevelObjects.First(x => x.name == upgradableComponent.owner[..firstEntryIndex]).transform, upgradableComponent.owner);

                    var actualComponent = objectOwner.GetComponent(upgradableComponent.type);

                    using var serializedObject = new SerializedObject(actualComponent);

                    serializedObject.Update();

                    foreach (var upgradableField in upgradableComponent.upgradableFields)
                    {
                        var originalProperty = serializedObject.FindProperty(upgradableField.originalField);
                        var targetProperty = serializedObject.FindProperty(upgradableField.targetField);

                        upgradableField.strategy.Migrate(originalProperty, targetProperty, upgradableField);
                    }

                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            EditorSceneManager.OpenScene(currentScene);
        }

        private static void ProcessComponents(IEnumerable<Component> components, List<UpgradableComponent> list)
        {
            foreach (var component in components)
            {
                var upgradableComponent = new UpgradableComponent
                {
                    owner = GetComponentOwnerPath(component),
                    type = component.GetType()
                };

                using var serializedObject = new SerializedObject(component);

                ProcessFields(serializedObject, upgradableComponent.type, upgradableComponent.upgradableFields);

                if (upgradableComponent.upgradableFields.Count == 0)
                    continue;

                list.Add(upgradableComponent);
            }
        }

        private static void ProcessFields(SerializedObject serializedObject, Type type, List<UpgradableField> list)
        {
            list.AddRange(from field in type.GetFields()
                let attr = field.GetCustomAttribute<MigrateFieldAttribute>()
                let args = field.FieldType.GetGenericArguments()
                let prop = serializedObject.FindProperty(field.Name)
                where attr != null
                select new UpgradableField
                {
                    originalField = field.Name,
                    targetField = attr.TargetField,
                    typeArguments = args,
                    isOverride = prop.prefabOverride,
                    isDefaultFromPrefab = prop.isInstantiatedPrefab && !prop.prefabOverride,
                    strategy = (IMigrationOperationStrategy)Activator.CreateInstance(Array.Find(_strategyCache, x => x.Name == attr.Strategy))
                }
            );
        }

        private static string GetComponentOwnerPath(Component component)
        {
            var currentGameObject = component.transform;

            if (!currentGameObject.parent)
                return currentGameObject.name;

            var path = currentGameObject.GetSiblingIndex().ToString();

            while (currentGameObject.transform.parent != component.transform.root)
            {
                currentGameObject = currentGameObject.transform.parent;

                path = $"{currentGameObject.GetSiblingIndex()}/{path}";
            }

            return $"{component.transform.root.gameObject.name}/{path}";
        }

        private static Transform GetChildFromPath(Transform root, string path)
        {
            var transformPath = path.Split("/", StringSplitOptions.RemoveEmptyEntries);
            var leaf = root;

            for (var i = 1; i < transformPath.Length; i++) // Discarding first entry as it is the name of the root
                leaf = leaf.GetChild(int.Parse(transformPath[i]));

            return leaf;
        }

        private static bool TypeIsComponent(Type type)
        {
            for (;;)
            {
                if (type.BaseType == null)
                    return false;

                if (type.BaseType == typeof(Component))
                    return true;

                type = type.BaseType;
            }
        }

        private static bool TypeIsScriptableObject(Type type)
        {
            for (;;)
            {
                if (type.BaseType == null)
                    return false;

                if (type.BaseType == typeof(ScriptableObject))
                    return true;

                type = type.BaseType;
            }
        }
    }
}