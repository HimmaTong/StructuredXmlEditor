﻿using StructuredXmlEditor.Definition;
using StructuredXmlEditor.View;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace StructuredXmlEditor.Data
{
	public abstract class DataItem : NotifyPropertyChanged, IDataGridItem
	{
		//-----------------------------------------------------------------------
		public int Index
		{
			get { return m_index; }
			set
			{
				if (m_index != value)
				{
					m_index = value;
					RaisePropertyChangedEvent();
				}
			}
		}

		//-----------------------------------------------------------------------
		IEnumerable<IDataGridItem> IDataGridItem.Items { get { return Children; } }

		//-----------------------------------------------------------------------
		IDataGridItem IDataGridItem.Parent
		{
			get { return Parent; }
		}

		//-----------------------------------------------------------------------
		bool IDataGridItem.IsVisible { get { return IsVisible; } }

		//-----------------------------------------------------------------------
		bool IDataGridItem.IsSelected { get; set; }

		//-----------------------------------------------------------------------
		public Command<object> FocusCMD { get { return new Command<object>((x) => FocusItem()); } }

		//-----------------------------------------------------------------------
		public ObservableCollection<DataItem> Children
		{
			get { return m_children; }
			set
			{
				if (m_children != null)
				{
					m_children.CollectionChanged -= OnChildrenCollectionChanged;
				}

				m_children = value;

				if (m_children != null)
				{
					m_children.CollectionChanged += OnChildrenCollectionChanged;
				}
			}
		}

		//-----------------------------------------------------------------------
		public IEnumerable<DataItem> Descendants
		{
			get
			{
				foreach (var child in Children)
				{
					yield return child;

					foreach (var item in child.Descendants)
					{
						yield return item;
					}
				}
			}
		}

		//-----------------------------------------------------------------------
		public DataItem Parent
		{
			get { return m_parent; }
			set
			{
				if (m_parent != value)
				{
					m_parent = value;

					if (m_parent != null)
					{
						Grid = m_parent.Grid;
					}

					RaisePropertyChangedEvent();

					UpdateVisibleIfBinding();
				}
			}
		}

		//-----------------------------------------------------------------------
		public bool IsVisible
		{
			get { return m_isVisible && !m_isSearchFiltered && !m_isMultiselectFiltered && IsVisibleFromBindings; }
			set
			{
				if (m_isVisible != value)
				{
					m_isVisible = value;

					RaisePropertyChangedEvent();
				}
			}
		}

		//-----------------------------------------------------------------------
		public bool IsVisibleFromBindings
		{
			get
			{
				foreach (var binding in VisibleIfStatements)
				{
					if (!binding.Evaluate()) return false;
				}
				return true;
			}
		}

		//-----------------------------------------------------------------------
		public virtual bool IsExpanded
		{
			get { return m_isExpanded; }
			set
			{
				if (m_isExpanded != value)
				{
					m_isExpanded = value;

					RaisePropertyChangedEvent();

					if (m_isExpanded)
					{
						OnExpanded();
					}
				}
			}
		}

		//-----------------------------------------------------------------------
		public virtual string Name
		{
			get { return m_name; }
			set
			{
				if (m_name != value)
				{
					m_name = value;

					RaisePropertyChangedEvent();
					RaisePropertyChangedEvent("FocusName");
				}
			}
		}

		//-----------------------------------------------------------------------
		public string FocusName
		{
			get
			{
				var name = Name;
				var desc = Description;

				if (string.IsNullOrWhiteSpace(desc)) return name;

				if (desc.Length > 13)
				{
					desc = desc.Substring(0, 10) + "...";
				}

				return name + ":" + desc;
			}
		}

		//-----------------------------------------------------------------------
		public bool IsSelected { get; set; }

		//-----------------------------------------------------------------------
		public bool HasParent { get { return Parent != null; } }

		//-----------------------------------------------------------------------
		public virtual string ToolTip { get; protected set; }

		//-----------------------------------------------------------------------
		public virtual Command<object> RemoveCMD { get { return null; } }

		//-----------------------------------------------------------------------
		public virtual bool IsCollectionChild { get { return false; } }

		//-----------------------------------------------------------------------
		public virtual bool CanReorder { get { return false; } }

		//-----------------------------------------------------------------------
		public int ZIndex
		{
			get { return m_zindex; }
			set
			{
				m_zindex = value;
				RaisePropertyChangedEvent();
			}
		}

		//-----------------------------------------------------------------------
		public Visibility FirstItem
		{
			get { return m_firstItem; }
			set
			{
				m_firstItem = value;
				RaisePropertyChangedEvent();
			}
		}

		//-----------------------------------------------------------------------
		public XmlDataGrid Grid
		{
			get
			{
				if (m_grid == null && Parent != null)
				{
					m_grid = Parent.Grid;
				}

				return m_grid;
			}
			set { m_grid = value; }
		}
		private XmlDataGrid m_grid;


		//-----------------------------------------------------------------------
		public ContextMenu ContextMenu
		{
			get
			{
				return CreateContextMenu();
			}
		}

		//-----------------------------------------------------------------------
		public DataDefinition Definition { get; set; }

		//-----------------------------------------------------------------------
		public abstract string Description { get; }

		//-----------------------------------------------------------------------
		public UndoRedoManager UndoRedo { get; set; }

		//-----------------------------------------------------------------------
		public virtual string CopyKey { get { return Definition.CopyKey; } }

		//-----------------------------------------------------------------------
		public virtual bool CanPaste { get { return true; } }

		//-----------------------------------------------------------------------
		public virtual bool CanRemove { get { return true; } }

		//-----------------------------------------------------------------------
		public virtual Command<object> PasteCMD { get { return new Command<object>((e) => Paste(), (e) => CanPaste && Clipboard.ContainsData(CopyKey)); } }

		//-----------------------------------------------------------------------
		public virtual bool IsPrimitive { get { return false; } }

		//-----------------------------------------------------------------------
		public List<Statement> VisibleIfStatements = new List<Statement>();

		//-----------------------------------------------------------------------
		public DataItem(DataDefinition definition, UndoRedoManager undoRedo)
		{
			Definition = definition;
			Name = definition.Name;
			this.UndoRedo = undoRedo;

			Children = new ObservableCollection<DataItem>();

			PropertyChanged += (e, a) =>
			{
				if (a.PropertyName == "Description")
				{
					RaisePropertyChangedEvent("FocusName");
				}
			};
		}

		//-----------------------------------------------------------------------
		private void UpdateVisibleIfBinding()
		{
			VisibleIfStatements.Clear();

			if (Definition.VisibleIf != null)
			{
				// decompose into boolean statements
				var statements = Definition.VisibleIf.Split(new string[] { "&&" }, StringSplitOptions.RemoveEmptyEntries);

				// extract the linked element and value from the boolean and setup the binding
				foreach (var statement in statements)
				{
					var stmnt = new Statement(statement);
					VisibleIfStatements.Add(stmnt);

					// find the referenced element and bind to it
					foreach (var child in Parent.Children)
					{
						if (child != this && child.Name == stmnt.TargetName)
						{
							stmnt.SetTarget(child);
							child.PropertyChanged += (e, a) =>
							{
								RaisePropertyChangedEvent("IsVisible");
							};

							break;
						}
					}
				}
			}
		}

		//-----------------------------------------------------------------------
		public abstract void Copy();

		//-----------------------------------------------------------------------
		public abstract void Paste();

		//-----------------------------------------------------------------------
		private ContextMenu CreateContextMenu()
		{
			ContextMenu menu = new ContextMenu();

			AddContextMenuItems(menu);

			MenuItem focusItem = new MenuItem();
			focusItem.Header = "Focus";

			focusItem.Click += delegate
			{
				Grid.FocusItem(this);
			};

			menu.Items.Add(focusItem);

			menu.Items.Add(new Separator());

			MenuItem collapseItem = new MenuItem();
			collapseItem.Header = "Collapse All";

			collapseItem.Click += delegate
			{
				foreach (var item in this.GetAllBreadthFirst())
				{
					item.IsExpanded = false;
				}
			};

			menu.Items.Add(collapseItem);

			MenuItem expandItem = new MenuItem();
			expandItem.Header = "Expand All";

			expandItem.Click += delegate
			{
				foreach (var item in this.GetAllBreadthFirst())
				{
					item.IsExpanded = true;
				}
			};

			menu.Items.Add(expandItem);

			return menu;
		}

		//-----------------------------------------------------------------------
		protected virtual void AddContextMenuItems(ContextMenu menu)
		{

		}

		//-----------------------------------------------------------------------
		void OnChildrenCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					{
						foreach (var i in e.NewItems.OfType<DataItem>())
						{
							i.Parent = this;
							i.Grid = Grid;
							i.PropertyChanged += ChildPropertyChanged;
						}

						break;
					}
				case NotifyCollectionChangedAction.Replace:
					{
						foreach (var i in e.NewItems.OfType<DataItem>())
						{
							i.Parent = this;
							i.Grid = Grid;
							i.PropertyChanged += ChildPropertyChanged;
						}

						foreach (var i in e.OldItems.OfType<DataItem>())
						{
							i.PropertyChanged -= ChildPropertyChanged;
						}

						break;
					}
				case NotifyCollectionChangedAction.Move:
					{
						break;
					}
				case NotifyCollectionChangedAction.Remove:
					{
						foreach (var i in e.OldItems.OfType<DataItem>())
						{
							i.PropertyChanged -= ChildPropertyChanged;
						}

						break;
					}
				case NotifyCollectionChangedAction.Reset:
					{
						break;
					}
				default:
					break;
			}

			for (int i = 0; i < Children.Count; ++i)
			{
				Children[i].Index = i;
			}

			IsExpanded = true;
		}

		//-----------------------------------------------------------------------
		public virtual void ChildPropertyChanged(object sender, PropertyChangedEventArgs args)
		{

		}

		//-----------------------------------------------------------------------
		public virtual void ParentPropertyChanged(object sender, PropertyChangedEventArgs e)
		{

		}

		//-----------------------------------------------------------------------
		public virtual bool Filter(string filter)
		{
			RefreshChildren();

			bool matchFound = false;

			if (Children.Count > 0)
			{
				foreach (var item in Children)
				{
					if (item.Filter(filter))
					{
						matchFound = true;
					}
				}
			}

			if (filter == null)
			{
				m_isSearchFiltered = false;
			}
			else if (!matchFound)
			{
				matchFound = Name.ToLower().Contains(filter);

				if (!matchFound)
				{
					matchFound = Description.ToLower().Contains(filter);
				}

				m_isSearchFiltered = !matchFound;
			}
			else
			{
				IsExpanded = true;
				m_isSearchFiltered = false;
			}

			RaisePropertyChangedEvent("IsVisible");

			return matchFound;
		}

		//-----------------------------------------------------------------------
		public void Focus()
		{
			RefreshChildren();
		}

		//-----------------------------------------------------------------------
		protected virtual void OnExpanded()
		{

		}

		//-----------------------------------------------------------------------
		protected virtual void RefreshChildren()
		{

		}

		//-----------------------------------------------------------------------
		protected void DeferredRefreshChildren(DispatcherPriority priority = DispatcherPriority.Normal)
		{
			if (m_deferredUpdateChildren != null)
			{
				m_deferredUpdateChildren.Abort();
				m_deferredUpdateChildren = null;
			}

			m_deferredUpdateChildren = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
			{
				RefreshChildren();
				m_deferredUpdateChildren = null;
			}));
		}

		//-----------------------------------------------------------------------
		protected void FocusItem()
		{
			Grid.FocusItem(this);
		}

		//-----------------------------------------------------------------------
		public IEnumerable<DataItem> ActiveDescendants()
		{
			foreach (var i in Children)
			{
				yield return i;

				foreach (var j in i.ActiveDescendants())
				{
					yield return j;
				}
			}
		}

		//-----------------------------------------------------------------------
		Visibility m_firstItem = Visibility.Hidden;
		int m_zindex = 0;
		bool m_isSearchFiltered = false;
		protected bool m_isMultiselectFiltered = false;
		string m_name;
		bool m_isExpanded = false;
		bool m_isVisible = true;
		DataItem m_parent = null;
		int m_index = -1;
		DispatcherOperation m_deferredUpdateChildren = null;
		private ObservableCollection<DataItem> m_children;
	}

	//-----------------------------------------------------------------------
	public class Statement
	{
		//-----------------------------------------------------------------------
		public enum ComparisonOperation
		{
			[Description("==")]
			Equal,
			[Description("!=")]
			NotEqual,
			[Description("<")]
			LessThan,
			[Description("<=")]
			LessThanOrEqual,
			[Description(">")]
			GreaterThan,
			[Description(">=")]
			GreaterThanOrEqual
		}

		//-----------------------------------------------------------------------
		public ComparisonOperation Operator { get; set; }
		public string TargetName { get; set; }
		public string TargetValue { get; set; }

		public DataItem Target { get; set; }

		//-----------------------------------------------------------------------
		public Statement(string statement)
		{
			foreach (ComparisonOperation op in Enum.GetValues(typeof(ComparisonOperation)))
			{
				string opString = op.GetDescription();
				if (statement.Contains(opString))
				{
					var split = statement.Split(new string[] { opString }, StringSplitOptions.RemoveEmptyEntries);
					TargetName = split[0].Trim();
					TargetValue = split[1].Trim();
					Operator = op;

					break;
				}
			}
		}

		//-----------------------------------------------------------------------
		public void SetTarget(DataItem target)
		{
			Target = target;

			if (!Target.IsPrimitive)
			{
				throw new Exception("Cannot do comparison operations on non-primitives!");
			}
			else if (!(target is NumberItem) && Operator != ComparisonOperation.Equal && Operator != ComparisonOperation.NotEqual)
			{
				throw new Exception("Invalid operation '" + Operator + "' on non-number item '" + target.Name + "'!");
			}
		}

		//-----------------------------------------------------------------------
		public bool Evaluate()
		{
			if (Target is NumberItem)
			{
				var val = (Target as NumberItem).Value;
				var target = float.Parse(TargetValue);

				if (Operator == ComparisonOperation.Equal) { return val == target; }
				else if (Operator == ComparisonOperation.NotEqual) { return val != target; }
				else if (Operator == ComparisonOperation.LessThan) { return val < target; }
				else if (Operator == ComparisonOperation.LessThanOrEqual) { return val <= target; }
				else if (Operator == ComparisonOperation.GreaterThan) { return val > target; }
				else if (Operator == ComparisonOperation.GreaterThanOrEqual) { return val >= target; }
				else { throw new Exception("Invalid operation type " + Operator + " for float!"); }
			}
			else if (Target is BooleanItem)
			{
				var val = (Target as BooleanItem).Value;
				var target = bool.Parse(TargetValue);

				if (Operator == ComparisonOperation.Equal) { return val == target; }
				else if (Operator == ComparisonOperation.NotEqual) { return val != target; }
				else { throw new Exception("Invalid operation type " + Operator + " for bool!"); }
			}
			else
			{
				var val = Target.GetType().GetProperty("Value").GetValue(Target) as string; // reflection cause cant cast to PrimitiveDataType<>
				var target = TargetValue;

				if (Operator == ComparisonOperation.Equal) { return val == target; }
				else if (Operator == ComparisonOperation.NotEqual) { return val != target; }
				else { throw new Exception("Invalid operation type " + Operator + " for string!"); }
			}
		}
	}
}
