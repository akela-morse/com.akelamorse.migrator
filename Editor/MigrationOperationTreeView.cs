using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace AkelaToolsEditor
{
	public class MigrationOperationTreeView : TreeView<int>
	{
		private readonly List<TreeViewItem<int>> _data;

		public MigrationOperationTreeView(TreeViewState<int> state, in IList<UpgradableScene> scenes, in IList<UpgradablePrefab> prefabs) : base(state)
		{
			_data = new List<TreeViewItem<int>>();

			var id = 0;

			for (var i = 0; i < prefabs.Count; ++i)
			{
				_data.Add(new TreeViewItem<int>
				{
					id = id++,
					icon = (Texture2D)EditorGUIUtility.IconContent(prefabs[i].isPrefabVariant ? "PrefabVariant Icon" : "Prefab Icon").image,
					depth = 0,
					displayName = $"{Path.GetFileNameWithoutExtension(prefabs[i].path)} ({prefabs[i].path})"
				});

				for (var j = 0; j < prefabs[i].components.Count; ++j)
				{
					var component = prefabs[i].components[j];

					_data.Add(new TreeViewItem<int>
					{
						id = id++,
						icon = (Texture2D)EditorGUIUtility.IconContent(component.isPrefabInstance ? "d_Prefab On Icon" : "GameObject Icon").image,
						depth = 1,
						displayName = $"{component.type.Name} ({component.owner})"
					});

					for (var k = 0; k < component.upgradableFields.Count; k++)
					{
						var field = component.upgradableFields[k];

						_data.Add(new TreeViewItem<int>
						{
							id = id++,
							icon = (Texture2D)EditorGUIUtility.IconContent(GetIconForField(field)).image,
							depth = 2,
							displayName = $"{field.originalField} -> {field.targetField} ({field.strategy.GetType().Name})"
						});
					}
				}
			}

			for (var i = 0; i < scenes.Count; ++i)
			{
				_data.Add(new TreeViewItem<int>
				{
					id = id++,
					icon = (Texture2D)EditorGUIUtility.IconContent("SceneAsset Icon").image,
					depth = 0,
					displayName = $"{Path.GetFileNameWithoutExtension(scenes[i].path)} ({scenes[i].path})"
				});

				for (var j = 0; j < scenes[i].components.Count; ++j)
				{
					var component = scenes[i].components[j];

					_data.Add(new TreeViewItem<int>
					{
						id = id++,
						icon = (Texture2D)EditorGUIUtility.IconContent(component.isPrefabInstance ? "d_Prefab On Icon" : "GameObject Icon").image,
						depth = 1,
						displayName = $"{component.type.Name} ({component.owner})"
					});

					for (var k = 0; k < component.upgradableFields.Count; k++)
					{
						var field = component.upgradableFields[k];

						_data.Add(new TreeViewItem<int>
						{
							id = id++,
							icon = (Texture2D)EditorGUIUtility.IconContent(GetIconForField(field)).image,
							depth = 2,
							displayName = $"{field.originalField} -> {field.targetField} ({field.strategy.GetType().Name})"
						});
					}
				}
			}

			Reload();
		}

		protected override TreeViewItem<int> BuildRoot()
		{
			var root = new TreeViewItem<int> { id = 0, depth = -1, displayName = "Root" };

			SetupParentsAndChildrenFromDepths(root, _data);

			return root;
		}

		private static string GetIconForField(UpgradableField field)
		{
			if (field.isOverride)
				return "sv_icon_dot1_pix16_gizmo";

			if (field.isDefaultFromPrefab)
				return "sv_icon_dot10_pix16_gizmo";

			return "sv_icon_dot2_pix16_gizmo";
		}
	}
}