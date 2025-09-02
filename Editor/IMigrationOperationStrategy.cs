using UnityEditor;

namespace MigratorEditor
{
	public interface IMigrationOperationStrategy
	{
		void Migrate(SerializedProperty originalProperty,  SerializedProperty targetProperty, UpgradableField fieldData);
	}
}