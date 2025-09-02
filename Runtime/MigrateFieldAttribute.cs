using System;

namespace Migrator
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MigrateFieldAttribute : Attribute
    {
		readonly string _targetField;

		public MigrateFieldAttribute(string targetField)
		{
			_targetField = targetField;
		}

		public string TargetField => _targetField;

        public string Strategy { get; set; }
    }
}