﻿using StructuredXmlEditor.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//-----------------------------------------------------------------------
public class UndoRedoDescription
{
	public bool IsUndo { get; set; }
	public bool IsRedo { get; set; }
	public string Description { get; set; }
	public int CountFromCurrent { get; set; }
	public bool IsSavePoint { get; set; }
	public bool IsCurrentPoint { get; set; }

	public UndoRedoDescription()
	{

	}
}

//-----------------------------------------------------------------------
public class UndoRedoManager : NotifyPropertyChanged
{
	public int GroupingMS { get; set; } = 500;

	public Stack<UndoRedoGroup> UndoStack = new Stack<UndoRedoGroup>();
	public Stack<UndoRedoGroup> RedoStack = new Stack<UndoRedoGroup>();

	public IEnumerable<UndoRedoDescription> DescriptionStack
	{
		get
		{
			var temp = new List<UndoRedoDescription>();
			int count = 0;
			foreach (var item in UndoStack)
			{
				temp.Add(new UndoRedoDescription()
				{
					Description = item.Desc,
					IsUndo = true,
					IsSavePoint = item == savePoint,
					IsCurrentPoint = count == 0,
					CountFromCurrent = count++
				});
			}

			temp.Reverse();
			foreach (var item in temp) yield return item;

			count = 1;
			foreach (var item in RedoStack)
			{
				yield return new UndoRedoDescription()
				{
					Description = item.Desc,
					IsSavePoint = item == savePoint,
					IsRedo = true,
					CountFromCurrent = count++
				};
			}
		}
	}

	public bool IsModified
	{
		get
		{
			if (savePoint == null)
			{
				return UndoStack.Count != 0;
			}
			else
			{
				return UndoStack.Peek() != savePoint;
			}
		}
	}

	public bool CanUndo
	{
		get { return UndoStack.Count > 0; }
	}

	public bool CanRedo
	{
		get { return RedoStack.Count > 0; }
	}

	public UsingContext DisableUndoScope()
	{
		--enableUndoRedo;

		return new UsingContext(() =>
		{
			++enableUndoRedo;
		});
	}

	public void ApplyDoUndo(Action _do, Action _undo, string _desc = "")
	{
		if (enableUndoRedo == 0)
		{
			if (isInApplyUndo)
			{
				Message.Show("Nested ApplyDoUndo calls! This is bad!", "Undo Redo borked", "Ok");
				_do();
				return;
			}

			isInApplyUndo = true;

			_do();

			RedoStack.Clear();

			var action = new UndoRedoAction(_do, _undo, _desc);

			if (UndoStack.Count > 0)
			{
				var lastGroup = UndoStack.Peek();

				var currentTime = DateTime.Now;
				var diff = (currentTime - lastGroup.LastActionTime).TotalMilliseconds;

				if (diff <= GroupingMS)
				{
					lastGroup.Actions.Add(action);
					lastGroup.LastActionTime = currentTime;
				}
				else
				{
					var group = new UndoRedoGroup();
					group.Actions.Add(action);
					group.LastActionTime = DateTime.Now;
					UndoStack.Push(group);
				}
			}
			else
			{
				var group = new UndoRedoGroup();
				group.Actions.Add(action);
				group.LastActionTime = DateTime.Now;
				UndoStack.Push(group);
			}

			RaisePropertyChangedEvent("IsModified");
			RaisePropertyChangedEvent("CanUndo");
			RaisePropertyChangedEvent("CanRedo");
			RaisePropertyChangedEvent("DescriptionStack");

			isInApplyUndo = false;
		}
		else
		{
			_do();
		}
	}

	public void Undo(int count)
	{
		for (int i = 0; i < count; i++)
		{
			Undo();
		}
	}

	public void Redo(int count)
	{
		for (int i = 0; i < count; i++)
		{
			Redo();
		}
	}

	public void Undo()
	{
		if (UndoStack.Count > 0)
		{
			var group = UndoStack.Pop();
			group.Undo();
			RedoStack.Push(group);

			RaisePropertyChangedEvent("IsModified");
			RaisePropertyChangedEvent("CanUndo");
			RaisePropertyChangedEvent("CanRedo");
			RaisePropertyChangedEvent("DescriptionStack");
		}
	}

	public void Redo()
	{
		if (RedoStack.Count > 0)
		{
			var group = RedoStack.Pop();
			group.Do();
			UndoStack.Push(group);

			RaisePropertyChangedEvent("IsModified");
			RaisePropertyChangedEvent("CanUndo");
			RaisePropertyChangedEvent("CanRedo");
			RaisePropertyChangedEvent("DescriptionStack");
		}
	}

	public void MarkSavePoint()
	{
		if (UndoStack.Count > 0)
		{
			savePoint = UndoStack.Peek();
		}
		else
		{
			savePoint = null;
		}

		RaisePropertyChangedEvent("IsModified");
		RaisePropertyChangedEvent("DescriptionStack");
	}

	int enableUndoRedo;
	UndoRedoGroup savePoint;
	bool isInApplyUndo;
}

//-----------------------------------------------------------------------
public class UndoRedoGroup
{
	public List<UndoRedoAction> Actions { get; set; } = new List<UndoRedoAction>();
	public DateTime LastActionTime { get; set; }

	public void Do()
	{
		foreach (var action in Actions) action.Do();
	}

	public void Undo()
	{
		var reversed = Actions.ToList();
		reversed.Reverse();
		foreach (var action in reversed) action.Undo();
	}

	public string Desc
	{
		get
		{
			return string.Join(",", Actions.Select(e => e.Desc).Distinct());
		}
	}
}

//-----------------------------------------------------------------------
public class UndoRedoAction
{
	public Action Do { get; set; }
	public Action Undo { get; set; }
	public string Desc { get; set; }

	public UndoRedoAction(Action _do, Action _undo, string _desc)
	{
		this.Do = _do;
		this.Undo = _undo;
		this.Desc = _desc;
	}
}

//-----------------------------------------------------------------------
public class UsingContext : IDisposable
{
	private Action action;

	public UsingContext(Action action)
	{
		this.action = action;
	}

	public void Dispose()
	{
		action();
	}
}