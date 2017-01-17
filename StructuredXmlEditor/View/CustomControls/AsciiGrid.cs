﻿using StructuredXmlEditor.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace StructuredXmlEditor.View
{
	public class AsciiGrid : Control
	{
		//-----------------------------------------------------------------------
		public Point ViewPos { get; set; }

		//-----------------------------------------------------------------------
		public int PixelsATile { get; set; } = 10;

		//-----------------------------------------------------------------------
		protected Brush BackgroundBrush { get { return (Application.Current.TryFindResource("WindowBackgroundBrush") as SolidColorBrush); } }

		//-----------------------------------------------------------------------
		protected Brush UsedAreaBrush { get { return (Application.Current.TryFindResource("BackgroundDarkBrush") as SolidColorBrush); } }

		//-----------------------------------------------------------------------
		protected Brush GridBrush { get { return (Application.Current.TryFindResource("BackgroundNormalBrush") as SolidColorBrush); } }

		//-----------------------------------------------------------------------
		protected Brush IndicatorBrush { get { return (Application.Current.TryFindResource("BorderDarkBrush") as SolidColorBrush); } }

		//-----------------------------------------------------------------------
		protected Brush SelectedBrush { get { return (Application.Current.TryFindResource("SelectionBorderBrush") as SolidColorBrush); } }

		//-----------------------------------------------------------------------
		protected Brush UnselectedBrush { get { return (Application.Current.TryFindResource("BorderLightBrush") as SolidColorBrush); } }

		//-----------------------------------------------------------------------
		protected Brush FontBrush { get { return (Application.Current.TryFindResource("FontDarkBrush") as SolidColorBrush); } }

		//-----------------------------------------------------------------------
		private MultilineStringItem Item { get { return DataContext as MultilineStringItem; } }

		//-----------------------------------------------------------------------
		private char[,] Grid { get; set; }
		private IntPoint ZeroPoint { get { return Item.ZeroPoint; } set { Item.ZeroPoint = value; } }

		//-----------------------------------------------------------------------
		private int GridWidth { get { return Grid?.GetLength(0) ?? 0; } }
		private int GridHeight { get { return Grid?.GetLength(1) ?? 0; } }

		//-----------------------------------------------------------------------
		private List<IntPoint> Selected { get; } = new List<IntPoint>() { new IntPoint(0, 0) };

		//-----------------------------------------------------------------------
		public AsciiGrid()
		{
			DataContextChanged += (e, args) =>
			{
				if (args.OldValue != null && args.OldValue is MultilineStringItem)
				{
					var oldItem = args.OldValue as MultilineStringItem;
					oldItem.PropertyChanged -= OnPropertyChange;
				}

				if (args.NewValue != null && args.NewValue is MultilineStringItem)
				{
					var newItem = args.NewValue as MultilineStringItem;
					newItem.PropertyChanged += OnPropertyChange;

					DatacontextChanged();
				}

				InvalidateVisual();
			};

			redrawTimer = new Timer();
			redrawTimer.Interval = 1.0 / 15.0;
			redrawTimer.Elapsed += (e, args) =>
			{
				if (m_dirty)
				{
					Application.Current.Dispatcher.BeginInvoke(new Action(() => { InvalidateVisual(); }));
					m_dirty = false;
				}
			};
			redrawTimer.Start();

			Focusable = true;
		}

		//-----------------------------------------------------------------------
		~AsciiGrid()
		{
			redrawTimer.Stop();
		}

		//-----------------------------------------------------------------------
		private void DatacontextChanged()
		{
			if (ActualWidth == 0 || double.IsNaN(ActualWidth) || ActualHeight == 0 || double.IsNaN(ActualHeight))
			{
				Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
				{
					DatacontextChanged();
				}));
			}
			else
			{
				UpdateGrid();
				ZoomToBestFit();
			}
		}

		//-----------------------------------------------------------------------
		private void UpdateGrid()
		{
			var lines = Item.Value.Split('\n');
			if (lines.Last() == "") lines = lines.Take(lines.Length - 1).ToArray();

			var width = lines.FirstOrDefault()?.Length ?? 0;
			var height = lines.Length;

			Grid = new char[width,height];

			for (int x = 0; x < GridWidth; x++)
			{
				for (int y = 0; y < GridHeight; y++)
				{
					var line = lines[y];
					if (line.Length > x)
					{
						Grid[x, y] = line[x];
					}
					else
					{
						Grid[x, y] = ' ';
					}
				}
			}

			m_dirty = true;
		}

		//-----------------------------------------------------------------------
		private void UpdateSrcGrid()
		{
			var value = "";

			for (int y = 0; y < GridHeight; y++)
			{
				for (int x = 0; x < GridWidth; x++)
				{
					value += Grid[x, y];
				}

				if (y < GridHeight-1) value += "\n";
			}

			Item.Value = value;
		}

		//-----------------------------------------------------------------------
		private void ExpandGrid(IntPoint point)
		{
			if (GridWidth == 0 && GridHeight == 0)
			{
				Grid = new char[1, 1];
				Grid[0, 0] = ' ';
				ZeroPoint = new IntPoint(-point.X, -point.Y);

				return;
			}

			var bounds = new Int32Rect(-ZeroPoint.X, -ZeroPoint.Y, GridWidth, GridHeight);

			if (point.X >= bounds.X && point.Y >= bounds.Y && point.X < bounds.X+bounds.Width && point.Y < bounds.Y+bounds.Height)
			{
				// in bounds, do nothing
				return;
			}

			var min = new IntPoint(Math.Min(point.X, bounds.X), Math.Min(point.Y, bounds.Y));
			var max = new IntPoint(Math.Max(point.X+1, bounds.X+bounds.Width), Math.Max(point.Y+1, bounds.Y+bounds.Height));

			if (min.X < bounds.X || min.Y < bounds.Y)
			{
				// move zero point
				ZeroPoint = new IntPoint(ZeroPoint.X + (bounds.X - min.X), ZeroPoint.Y + (bounds.Y - min.Y));

				var newGrid = new char[max.X - min.X, max.Y - min.Y];
				for (int x = 0; x < newGrid.GetLength(0); x++)
				{
					for (int y = 0; y < newGrid.GetLength(1); y++)
					{
						newGrid[x, y] = ' ';
					}
				}

				for (int x = 0; x < GridWidth; x++)
				{
					for (int y = 0; y < GridHeight; y++)
					{
						newGrid[(bounds.X - min.X) + x, (bounds.Y - min.Y) + y] = Grid[x, y];
					}
				}

				Grid = newGrid;
			}
			else
			{
				var newGrid = new char[max.X - min.X, max.Y - min.Y];
				for (int x = 0; x < newGrid.GetLength(0); x++)
				{
					for (int y = 0; y < newGrid.GetLength(1); y++)
					{
						newGrid[x, y] = ' ';
					}
				}

				for (int x = 0; x < GridWidth; x++)
				{
					for (int y = 0; y < GridHeight; y++)
					{
						newGrid[x, y] = Grid[x, y];
					}
				}

				Grid = newGrid;
			}

			UpdateSrcGrid();

			m_dirty = true;
		}

		//-----------------------------------------------------------------------
		private void ContractGrid()
		{
			IntPoint min = new IntPoint(int.MaxValue, int.MaxValue);
			IntPoint max = new IntPoint(0, 0);

			for (int x = 0; x < GridWidth; x++)
			{
				for (int y = 0; y < GridHeight; y++)
				{
					if (Grid[x, y] != ' ')
					{
						if (x < min.X) min = new IntPoint(x, min.Y);
						if (y < min.Y) min = new IntPoint(min.X, y);
						if (x > max.X) max = new IntPoint(x, max.Y);
						if (y > max.Y) max = new IntPoint(max.X, y);
					}
				}
			}

			if (min.X > max.X && min.Y > max.Y)
			{
				// its emtpy!
				ZeroPoint = new IntPoint(0, 0);
				Grid = new char[0, 0];

				return;
			}

			if (min.X != 0 || min.Y != 0)
			{
				// move zero point
				ZeroPoint = new IntPoint(ZeroPoint.X - min.X, ZeroPoint.Y - min.Y);

				var newGrid = new char[(max.X - min.X) + 1, (max.Y - min.Y) + 1];
				for (int x = 0; x < newGrid.GetLength(0); x++)
				{
					for (int y = 0; y < newGrid.GetLength(1); y++)
					{
						newGrid[x, y] = Grid[x + min.X, y + min.Y];
					}
				}

				Grid = newGrid;
			}
			else if (max.X != GridWidth || max.Y != GridHeight)
			{
				// shave off edge
				var newGrid = new char[(max.X - min.X) + 1, (max.Y - min.Y) + 1];
				for (int x = 0; x < newGrid.GetLength(0); x++)
				{
					for (int y = 0; y < newGrid.GetLength(1); y++)
					{
						newGrid[x, y] = Grid[x, y];
					}
				}

				Grid = newGrid;
			}
		}

		//-----------------------------------------------------------------------
		private void Delete(IntPoint point)
		{
			var bounds = new Int32Rect(-ZeroPoint.X, -ZeroPoint.Y, GridWidth, GridHeight);

			if (point.X >= bounds.X && point.Y >= bounds.Y && point.X < bounds.X + bounds.Width && point.Y < bounds.Y + bounds.Height)
			{
				// only delete when in bounds
				Grid[point.X + ZeroPoint.X, point.Y + ZeroPoint.Y] = ' ';
			}
		}

		//-----------------------------------------------------------------------
		private void OnPropertyChange(object sender, PropertyChangedEventArgs args)
		{
			if (args.PropertyName == "Value")
			{
				UpdateGrid();
			}
		}

		//-----------------------------------------------------------------------
		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);

			drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));

			drawingContext.DrawRectangle(BackgroundBrush, null, new System.Windows.Rect(0, 0, ActualWidth, ActualHeight));

			// draw used area
			drawingContext.DrawRectangle(UsedAreaBrush, null, new System.Windows.Rect(-ViewPos.X - (ZeroPoint.X * PixelsATile), -ViewPos.Y - (ZeroPoint.Y * PixelsATile), GridWidth * PixelsATile, GridHeight * PixelsATile));

			// draw grid lines
			var startX = (Math.Floor(ViewPos.X / PixelsATile) * PixelsATile) - ViewPos.X;
			var startY = (Math.Floor(ViewPos.Y / PixelsATile) * PixelsATile) - ViewPos.Y;

			var gridPen = new Pen(GridBrush, 1);
			gridPen.Freeze();

			var selectedPen = new Pen(SelectedBrush, 1);
			selectedPen.Freeze();

			Typeface typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);

			for (double x = startX; x < ActualWidth; x += PixelsATile)
			{
				drawingContext.DrawLine(gridPen, new Point(x, 0), new Point(x, ActualHeight));
			}

			for (double y = startY; y < ActualHeight; y += PixelsATile)
			{
				drawingContext.DrawLine(gridPen, new Point(0, y), new Point(ActualWidth, y));
			}

			// draw characters
			for (int x = 0; x < GridWidth; x++)
			{
				for (int y = 0; y < GridHeight; y++)
				{
					FormattedText text = new FormattedText(Grid[x, y].ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, PixelsATile, FontBrush);
					Point centerPoint = new Point((x-ZeroPoint.X) * PixelsATile + PixelsATile / 2 - ViewPos.X, (y-ZeroPoint.Y) * PixelsATile - PixelsATile / 4 - ViewPos.Y);
					Point textLocation = new Point(centerPoint.X - text.WidthIncludingTrailingWhitespace / 2, centerPoint.Y);

					drawingContext.DrawText(text, textLocation);
				}
			}

			foreach (var point in Selected)
			{
				drawingContext.DrawRectangle(null, selectedPen, new Rect(point.X * PixelsATile - ViewPos.X, point.Y * PixelsATile - ViewPos.Y, PixelsATile, PixelsATile));
			}
		}

		//-----------------------------------------------------------------------
		protected override void OnMouseWheel(MouseWheelEventArgs e)
		{
			var pos = e.GetPosition(this);

			var local = new Point((pos.X + ViewPos.X) / PixelsATile, (pos.Y + ViewPos.Y) / PixelsATile);

			PixelsATile += (e.Delta / 120);
			if (PixelsATile < 5) PixelsATile = 5;

			ViewPos = new Point(local.X * PixelsATile - pos.X, local.Y * PixelsATile - pos.Y);

			InvalidateVisual();

			e.Handled = true;

			base.OnMouseWheel(e);
		}

		//-----------------------------------------------------------------------
		protected override void OnMouseDown(MouseButtonEventArgs e)
		{
			var pos = e.GetPosition(this);

			if (e.MiddleButton == MouseButtonState.Pressed)
			{
				panPos = pos;
				isPanning = true;
			}

			Keyboard.Focus(this);
		}

		//-----------------------------------------------------------------------
		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs args)
		{
			base.OnMouseLeftButtonDown(args);

			var pos = args.GetPosition(this);

			var local = new Point((pos.X + ViewPos.X) / PixelsATile, (pos.Y + ViewPos.Y) / PixelsATile);
			var roundedX = local.X < 0 ? (int)Math.Floor(local.X) : (int)local.X;
			var roundedY = local.Y < 0 ? (int)Math.Floor(local.Y) : (int)local.Y;

			startPos = new IntPoint(roundedX, roundedY);
			marqueeStart = pos;

			if (!Keyboard.IsKeyDown(Key.LeftCtrl))
			{
				Selected.Clear();
				Selected.Add(new IntPoint(roundedX, roundedY));
			}
			else
			{
				var existing = Selected.Where(e => e.X == roundedX && e.Y == roundedY).Cast<IntPoint?>().FirstOrDefault();
				if (existing != null)
				{
					Selected.Remove(existing.Value);
				}
				else
				{
					Selected.Add(new IntPoint(roundedX, roundedY));
				}
			}

			
			isMarqueeSelecting = true;

			m_dirty = true;
		}

		//-----------------------------------------------------------------------
		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (e.LeftButton != MouseButtonState.Pressed && e.MiddleButton != MouseButtonState.Pressed)
			{
				EndDrag();
				isMarqueeSelecting = false;
			}

			var pos = e.GetPosition(this);

			if (e.MiddleButton == MouseButtonState.Pressed && isPanning)
			{
				var diff = pos - panPos;
				ViewPos -= diff;
				m_dirty = true;

				if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
				{
					CaptureMouse();
					Mouse.OverrideCursor = Cursors.ScrollWE;
				}

				panPos = pos;
			}
			else if (e.LeftButton == MouseButtonState.Pressed && isMarqueeSelecting)
			{
				var diff = pos - marqueeStart;

				if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
				{
					var local = new Point((pos.X + ViewPos.X) / PixelsATile, (pos.Y + ViewPos.Y) / PixelsATile);
					var roundedX = local.X < 0 ? (int)Math.Floor(local.X) : (int)local.X;
					var roundedY = local.Y < 0 ? (int)Math.Floor(local.Y) : (int)local.Y;

					Selected.Clear();

					var min = new IntPoint(Math.Min(startPos.X, roundedX), Math.Min(startPos.Y, roundedY));
					var max = new IntPoint(Math.Max(startPos.X, roundedX), Math.Max(startPos.Y, roundedY));

					for (int x = min.X; x <= max.X; x++)
					{
						for (int y = min.Y; y <= max.Y; y++)
						{
							Selected.Add(new IntPoint(x, y));
						}
					}

					m_dirty = true;
				}
			}

			InvalidateVisual();
		}

		//-----------------------------------------------------------------------
		protected override void OnPreviewMouseUp(MouseButtonEventArgs args)
		{
			var pos = args.GetPosition(this);

			isMarqueeSelecting = false;

			EndDrag();
		}

		//-----------------------------------------------------------------------
		public void EndDrag()
		{
			bool wasDragging = isDragging;
			isDragging = false;
			isPanning = false;

			ReleaseMouseCapture();

			Mouse.OverrideCursor = null;
		}

		//-----------------------------------------------------------------------
		private void ZoomToBestFit()
		{
			if (ActualWidth == 0 || ActualHeight == 0)
			{
				return;
			}

			var xSize = ActualWidth / (GridWidth + 2);
			var ySize = ActualHeight / (GridHeight + 2);

			PixelsATile = (int)Math.Min(xSize, ySize);
			if (PixelsATile < 5) PixelsATile = 5;

			var visibleTilesX = (int)(ActualWidth / PixelsATile);
			var visibleTilesY = (int)(ActualHeight / PixelsATile);

			var padTilesX = (visibleTilesX - GridWidth) / 2;
			var padTilesY = (visibleTilesY - GridHeight) / 2;

			ViewPos = new Point(-PixelsATile * padTilesX, -PixelsATile * padTilesY);
		}

		//-----------------------------------------------------------------------
		protected override void OnKeyDown(KeyEventArgs args)
		{
			base.OnKeyDown(args);

			if (args.Key == Key.Left)
			{
				for (int i = 0; i < Selected.Count; i++)
				{
					Selected[i] = new IntPoint(Selected[i].X - 1, Selected[i].Y);
				}

				var viewMinX = Selected.Select(e => e.X).Min() * PixelsATile - ViewPos.X;
				if (viewMinX < 0)
				{
					ViewPos = new Point(ViewPos.X - PixelsATile, ViewPos.Y);
				}
			}
			else if (args.Key == Key.Up)
			{
				for (int i = 0; i < Selected.Count; i++)
				{
					Selected[i] = new IntPoint(Selected[i].X, Selected[i].Y - 1);
				}

				var viewMinY = Selected.Select(e => e.Y).Min() * PixelsATile - ViewPos.Y;
				if (viewMinY < 0)
				{
					ViewPos = new Point(ViewPos.X, ViewPos.Y - PixelsATile);
				}
			}
			else if (args.Key == Key.Right)
			{
				for (int i = 0; i < Selected.Count; i++)
				{
					Selected[i] = new IntPoint(Selected[i].X + 1, Selected[i].Y);
				}

				var viewMaxX = Selected.Select(e => e.X).Max() * PixelsATile - ViewPos.X;
				if (viewMaxX + PixelsATile > ActualWidth)
				{
					ViewPos = new Point(ViewPos.X + PixelsATile, ViewPos.Y);
				}
			}
			else if (args.Key == Key.Down)
			{
				for (int i = 0; i < Selected.Count; i++)
				{
					Selected[i] = new IntPoint(Selected[i].X, Selected[i].Y + 1);
				}

				var viewMaxY = Selected.Select(e => e.Y).Max() * PixelsATile - ViewPos.Y;
				if (viewMaxY + PixelsATile > ActualHeight)
				{
					ViewPos = new Point(ViewPos.X, ViewPos.Y + PixelsATile);
				}
			}
			else if (args.Key == Key.Delete)
			{
				foreach (var point in Selected)
				{
					Delete(point);
				}

				ContractGrid();
				UpdateSrcGrid();

				for (int i = 0; i < Selected.Count; i++)
				{
					Selected[i] = new IntPoint(Selected[i].X + 1, Selected[i].Y);
				}

				var viewMaxX = Selected.Select(e => e.X).Max() * PixelsATile - ViewPos.X;
				if (viewMaxX + PixelsATile > ActualWidth)
				{
					ViewPos = new Point(ViewPos.X + PixelsATile, ViewPos.Y);
				}
			}
			else
			{
				string rawResult = KeyCodeToUnicode(args.Key);

				if (!string.IsNullOrWhiteSpace(rawResult))
				{
					char key = rawResult.FirstOrDefault();

					IntPoint min = new IntPoint(int.MaxValue, int.MaxValue);
					IntPoint max = new IntPoint(-int.MaxValue, -int.MaxValue);

					foreach (var point in Selected)
					{
						if (point.X < min.X) min = new IntPoint(point.X, min.Y);
						if (point.Y < min.Y) min = new IntPoint(min.X, point.Y);
						if (point.X > max.X) max = new IntPoint(point.X, max.Y);
						if (point.Y > max.Y) max = new IntPoint(max.X, point.Y);
					}

					ExpandGrid(min);
					ExpandGrid(max);

					foreach (var point in Selected)
					{
						Grid[point.X + ZeroPoint.X, point.Y + ZeroPoint.Y] = key;
					}

					UpdateSrcGrid();

					for (int i = 0; i < Selected.Count; i++)
					{
						Selected[i] = new IntPoint(Selected[i].X + 1, Selected[i].Y);
					}

					var viewMaxX = Selected.Select(e => e.X).Max() * PixelsATile - ViewPos.X;
					if (viewMaxX + PixelsATile > ActualWidth)
					{
						ViewPos = new Point(ViewPos.X + PixelsATile, ViewPos.Y);
					}
				}
			}

			m_dirty = true;
		}

		//-----------------------------------------------------------------------
		public string KeyCodeToUnicode(Key key)
		{
			byte[] keyboardState = new byte[255];
			bool keyboardStateStatus = GetKeyboardState(keyboardState);

			if (!keyboardStateStatus)
			{
				return "";
			}

			uint virtualKeyCode = (uint)KeyInterop.VirtualKeyFromKey(key);
			uint scanCode = MapVirtualKey(virtualKeyCode, 0);
			IntPtr inputLocaleIdentifier = GetKeyboardLayout(0);

			StringBuilder result = new StringBuilder();
			ToUnicodeEx(virtualKeyCode, scanCode, keyboardState, result, (int)5, (uint)0, inputLocaleIdentifier);

			return result.ToString();
		}

		//-----------------------------------------------------------------------
		[DllImport("user32.dll")]
		static extern bool GetKeyboardState(byte[] lpKeyState);

		[DllImport("user32.dll")]
		static extern uint MapVirtualKey(uint uCode, uint uMapType);

		[DllImport("user32.dll")]
		static extern IntPtr GetKeyboardLayout(uint idThread);

		[DllImport("user32.dll")]
		static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);

		//-----------------------------------------------------------------------
		IntPoint startPos;
		Point marqueeStart;
		bool isMarqueeSelecting;

		Point panPos;
		bool isDragging = false;
		bool isPanning = false;
		private bool m_dirty;
		Timer redrawTimer;
	}

	public struct IntPoint
	{
		public int X { get; set; }
		public int Y { get; set; }

		public IntPoint(int x, int y)
		{
			this.X = x;
			this.Y = y;
		}
	}
}
