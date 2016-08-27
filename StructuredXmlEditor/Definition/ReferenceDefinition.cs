﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using StructuredXmlEditor.Data;

namespace StructuredXmlEditor.Definition
{
	public class ReferenceDefinition : ComplexDataDefinition
	{
		public List<string> Keys { get; set; } = new List<string>();
		public Dictionary<string, DataDefinition> Definitions { get; set; } = new Dictionary<string, DataDefinition>();
		public bool IsNullable { get; set; }

		public override DataItem CreateData(UndoRedoManager undoRedo)
		{
			var item = new ReferenceItem(this, undoRedo);
			if (Definitions.Count == 1 && !IsNullable)
			{
				item.ChosenDefinition = Definitions.Values.First();
				item.Create();
			}
			return item;
		}

		public override void DoSaveData(XElement parent, DataItem item)
		{
			var si = item as ReferenceItem;
			if (si.ChosenDefinition != null)
			{
				si.ChosenDefinition.DoSaveData(parent, si.WrappedItem);

				if (parent.Elements().Count() == 0) return;

				var el = parent.Elements().Last();
				el.SetAttributeValue("RefKey", si.ChosenDefinition.Name);
			}
		}

		public override DataItem LoadData(XElement element, UndoRedoManager undoRedo)
		{
			var key = element.Attribute("RefKey").Value.ToString();
			var def = Definitions[key];

			var item = new ReferenceItem(this, undoRedo);
			item.ChosenDefinition = def;

			var loaded = def.LoadData(element, undoRedo);
			item.WrappedItem = loaded;

			return item;
		}

		public override void Parse(XElement definition)
		{
			Keys.AddRange(definition.Attribute("Keys").Value.ToString().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
			IsNullable = TryParseBool(definition, "Nullable", true);
		}

		public override void RecursivelyResolve(Dictionary<string, DataDefinition> defs)
		{
			foreach (var key in Keys)
			{
				if (!defs.ContainsKey(key.ToLower())) throw new Exception("Failed to find key " + key + "!");
				Definitions[key] = defs[key.ToLower()];
			}
		}

		public override bool IsDefault(DataItem item)
		{
			return (item as ReferenceItem).ChosenDefinition == null;
		}
	}
}
