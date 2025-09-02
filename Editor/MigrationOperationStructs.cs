using System;
using System.Collections.Generic;

namespace MigratorEditor
{
	public struct UpgradablePrefab
	{
		public string path;
		public List<UpgradableComponent> components;
		public bool isPrefabVariant;
	}

	public struct UpgradableScene
	{
		public string path;
		public List<UpgradableComponent> components;
	}

	public struct UpgradableComponent
	{
		public string owner;
		public Type type;
		public List<UpgradableField> upgradableFields;
		public bool isPrefabInstance;
		public bool isPrefabVariant;
	}

    public struct UpgradableScriptableObject
    {
        public string path;
        public Type type;
        public List<UpgradableField> upgradableFields;
    }

	public struct UpgradableField
	{
		public string originalField;
		public string targetField;
		public Type[] typeArguments;
		public bool isOverride;
		public bool isDefaultFromPrefab;
		public IMigrationOperationStrategy strategy;
	}
}