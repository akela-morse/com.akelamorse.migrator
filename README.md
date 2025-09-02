# Migrator

Helper tool for Unity to refactor and migrate components.

## How to use

Let's say you want to migrate a `float` field in a particular `MonoBehaviour` or `ScriptableObject` into a `Vector3` field, and update the entirety of your project (prefabs, scenes, assets).

### Step 1: Create a Migration Strategy

Create a new Editor class that implements the `IMigrationOperationStrategy` interface, and implement the `Migrate` method.

```c#
public class FloatToVectorMigrationStrategy : IMigrationOperationStrategy
{
    public void Migrate(SerializedProperty originalProperty, SerializedProperty targetProperty, UpgradableField fieldData)
    {
        var value = originalProperty.floatValue;
        targetProperty.vector3Value = new Vector3(value, value, value);
    }
}
```

### Step 2: Add a `MigrateFieldAttribute` to the fields that need to be upgraded

The first parameter is the name of the new `Vector3` field that you want to migrate to. The second argument is the name of the Strategy class that you defined earlier.

```c#
public class MyBehaviour : MonoBehaviour
{
    [MigrateField("m_NewWayOfDoingThings", Strategy = "FloatToVectorMigrationStrategy")
    [SerializeField] private float m_OldWayOfDoingThings;

    [SerializeField] private Vector3 m_NewWayOfDoingThings;
}
```

### Step 3: Proceed with the migration

Open the Migrator window (Tools -> Migration Operation) and press Refresh. Migrator will go through your entire project, including prefabs and scenes, and list every single field to be migrated according to your specifications. Double-check that everything is in order, then press Migrate and go grab some coffee.


Once you're done with your migration, you may delete the strategy class if you want, as well as remove the obsolete fields from your classes.
