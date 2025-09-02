using UnityEditor;

namespace AkelaToolsEditor
{
	public interface IMigrationOperationStrategy
	{
		void Migrate(SerializedProperty originalProperty,  SerializedProperty targetProperty, UpgradableField fieldData);
	}
}
