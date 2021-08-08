using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using PoEAssetReader;
using PoEAssetReader.DatFiles;
using PoEAssetReader.DatFiles.Definitions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace PoEAssetVisualizer
{
	public partial class MainWindow : Window
	{
		#region Consts

		private const string RootNodeName = "ROOT";

		private const string DatDefinitionFileName = "DatDefinitions.xml";

		#endregion

		#region Variables

		private readonly string _poeDirectory;
		private AssetIndex _assetIndex;

		private AssetFile _openedAssetFile;
		private DatDefinitions _datDefinitions;
		private FileSystemWatcher _datDefinitionsWatcher;

		private Dictionary<string, HashSet<string>> _fileDirectories
		{
			get;
		} = new Dictionary<string, HashSet<string>>()
		{
			{ RootNodeName, new HashSet<string>() }
		};

		private readonly Stack<Cursor> _cursorsStack = new Stack<Cursor>();

		#endregion

		#region Lifecycle

		public MainWindow()
		{
			InitializeComponent();

			_poeDirectory = FindPoEDirectory();
			if(string.IsNullOrEmpty(_poeDirectory))
			{
				Application.Current.Shutdown();
				return;
			}

			SearchText.Text = Settings.Default.SearchText;
		}

		#endregion

		#region Private Methods

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			PushCursor(Cursors.Wait);
			HideAllViewers();

			new Thread(() =>
			{
				_assetIndex = new AssetIndex(_poeDirectory);

				var assetFiles = _assetIndex.FindFiles(x => true);

				foreach(var assetFile in assetFiles)
				{
					var exploded = assetFile.Name.Split(Path.AltDirectorySeparatorChar);
					if(exploded.Length > 1)
					{
						_fileDirectories[RootNodeName].Add(exploded[0]);
					}

					for(int i = 0; i < exploded.Length; i++)
					{
						var dir = string.Join(Path.AltDirectorySeparatorChar, exploded[..i]);
						if(!_fileDirectories.TryGetValue(dir, out var fileList))
						{
							fileList = new HashSet<string>();
							_fileDirectories[dir] = fileList;
						}
						fileList.Add(string.Join(Path.AltDirectorySeparatorChar, exploded[..(i + 1)]));
					}
				}

				Dispatcher.BeginInvoke(new Action(() =>
				{
					var root = CreateTreeViewItem(RootNodeName);
					AssetIndexTree.Items.Add(root);

					root.Items.Add(null);
					root.Expanded += TreeViewItem_Expanded;
					root.IsExpanded = true;

					PopCursor();
				}));
			}).Start();
		}

		private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
		{
			if(sender is TreeViewItem view && view.Tag is string parent)
			{
				if(view.Items.Count == 1 && view.Items[0] is null)
				{
					PushCursor(Cursors.Wait);
					view.Items.Clear();
					foreach(var child in _fileDirectories[parent].OrderBy(x => _fileDirectories.ContainsKey(x) ? $"!!{x}" : x))
					{
						var sub = CreateTreeViewItem(child);

						view.Items.Add(sub);
						if(_fileDirectories.ContainsKey(child))
						{
							sub.Items.Add(null);
							sub.Expanded += TreeViewItem_Expanded;
						}
						else
						{
							sub.MouseDoubleClick += Sub_MouseDoubleClick;
						}
					}
					UpdateTreeViewItemVisibility();
					PopCursor();
				}
			}
		}

		private void Sub_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			if(sender is TreeViewItem item && item.Tag is string file)
			{
				PushCursor(Cursors.Wait);

				HideAllViewers();
				if(_datDefinitionsWatcher != null)
				{
					_datDefinitionsWatcher.Dispose();
					_datDefinitionsWatcher = null;
				}
				ExportButton.IsEnabled = false;

				new Thread(() =>
				{
					AssetFile assetFile = _assetIndex.FindFile(x => x.Name == file);
					if(assetFile != null)
					{
						_openedAssetFile = assetFile;

						var contents = assetFile.GetFileContents();

						Dispatcher.BeginInvoke(new Action(() =>
						{
							FileLabel.Text = $"File: {file}";
							BundleLabel.Text = $"Bundle: {assetFile.Bundle.Name}";

							var extension = Path.GetExtension(file);
							switch(extension)
							{
								case ".dat":
								case ".dat64":
									FillDatViewer(_openedAssetFile);
									/*_datDefinitionsWatcher = new FileSystemWatcher(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), DatDefinitionFileName)
									{
										NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
									};
									_datDefinitionsWatcher.Changed += OnDatDefinitionsFileChanged;
									_datDefinitionsWatcher.EnableRaisingEvents = true;*/

									HexViewer.Stream = new MemoryStream(contents);
									HexViewerTab.Visibility = Visibility.Visible;
									break;

								case ".txt":
									TextViewer.Text = Encoding.UTF8.GetString(contents);
									TextViewerTab.Visibility = Visibility.Visible;
									break;

								default:
									HexViewer.Stream = new MemoryStream(contents);
									HexViewerTab.Visibility = Visibility.Visible;
									break;
							}

							ExportButton.IsEnabled = true;
							Viewers.Visibility = Visibility.Visible;
							foreach(TabItem tab in Viewers.Items)
							{
								if(tab.Visibility == Visibility.Visible)
								{
									Viewers.SelectedIndex = Viewers.Items.IndexOf(tab);
									break;
								}
							}
							PopCursor();
						}));
					}
					else
					{
						Dispatcher.BeginInvoke(new Action(() =>
						{
							PopCursor();
						}));
					}
				}).Start();
			}
		}

		private void OnDatDefinitionsFileChanged(object sender, FileSystemEventArgs e)
		{
			Dispatcher.BeginInvoke(new Action(() =>
			{
				PushCursor(Cursors.Wait);

				_datDefinitions = null;
				FillDatViewer(_openedAssetFile);

				PopCursor();
			}));
		}

		private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateTreeViewItemVisibility();
			Settings.Default.SearchText = SearchText.Text;
			Settings.Default.Save();
		}

		private void SearchInFileTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if(DatViewer.IsVisible)
			{
				ApplyDatViewerFilter();
				DatViewer.Items.Refresh();
			}
		}

		private void ApplyDatViewerFilter()
		{
			string searchText = SearchInFileText.Text;
			if(string.IsNullOrEmpty(searchText))
			{
				DatViewer.Items.Filter = null;
			}
			else
			{
				Regex regex = new Regex(searchText, RegexOptions.IgnoreCase);
				DatViewer.Items.Filter = o =>
				{
					IDictionary<string, object> row = (IDictionary<string, object>)(ExpandoObject)o;
					foreach((_, object value) in row)
					{
						if(regex.IsMatch(value.ToString()))
						{
							return true;
						}
					}
					return false;
				};
			}
		}

		private void FillDatViewer(AssetFile assetFile)
		{
			DatViewer.Columns.Clear();
			DatViewer.Items.Clear();
			DatViewerError.Text = string.Empty;
			DatViewerErrorTab.Visibility = Visibility.Collapsed;

			try
			{
				if(_datDefinitions == null)
				{
					_datDefinitions = DatDefinitions.ParsePyPoE();
				}
				DatFile datFile = new DatFile(assetFile, _datDefinitions);

				DatViewer.Columns.Add(new DataGridTextColumn()
				{
					Header = "#",
					Binding = new Binding("Index")
				});
				foreach(var field in datFile.FileDefinition.Fields)
				{
					AddColumn(field.ID);
				}
				TryAddColumn("_Remainder");
				TryAddColumn("_RemainderBool");
				TryAddColumn("_RemainderByte");
				TryAddColumn("_RemainderInt");
				TryAddColumn("_RemainderUInt");
				TryAddColumn("_RemainderLong");
				TryAddColumn("_RemainderULong");
				TryAddColumn("_RemainderFloat");
				TryAddColumn("_RemainderString");
				TryAddColumn("_RemainderRefString");
				TryAddColumn("_RemainderListULong");
				TryAddColumn("_RemainderListInt");

				void TryAddColumn(string columnName)
				{
					if (datFile.Records.Count > 0 && datFile.Records[0].HasValue(columnName))
					{
						AddColumn(columnName);
					}
				}

				void AddColumn(string columnName)
				{
					Style style = new Style(typeof(DataGridCell));
					style.Setters.Add(new Setter(ToolTipService.ToolTipProperty, new Binding($"{columnName}_Tooltip")));

					var column = new DataGridTextColumn()
					{
						Header = columnName,
						Binding = new Binding(columnName),
						CellStyle = style,
					};

					DatViewer.Columns.Add(column);
				}

				for (int i = 0; i < datFile.Records.Count; i++)
				{
					var record = datFile.Records[i];

					dynamic row = new ExpandoObject();
					IDictionary<string, object> rowDict = (IDictionary<string, object>)row;
					rowDict["Index"] = i;

					foreach(var key in record.Values.Keys)
					{
						rowDict[key] = record.GetStringValue(key);
						string remark = record.GetRemark(key);
						if(!string.IsNullOrEmpty(remark))
						{
							rowDict[$"{key}_Tooltip"] = remark;
						}
					}
					DatViewer.Items.Add(row);
				}

				ApplyDatViewerFilter();

				DatViewerTab.Visibility = Visibility.Visible;
			}
			catch(Exception ex)
			{
				string message = ex.Message;
				while(ex.InnerException != null)
				{
					message = $"{message} >> {ex.InnerException.Message}";
					ex = ex.InnerException;
				}
				DatViewerError.Text = $"Failed to parse the .dat file: {message}";
				DatViewerTab.Visibility = Visibility.Collapsed;
				DatViewerErrorTab.Visibility = Visibility.Visible;
			}
		}

		private void HideAllViewers()
		{
			Viewers.Visibility = Visibility.Hidden;
			HexViewerTab.Visibility = Visibility.Collapsed;
			DatViewerTab.Visibility = Visibility.Collapsed;
			DatViewerErrorTab.Visibility = Visibility.Collapsed;
			TextViewerTab.Visibility = Visibility.Collapsed;
		}

		private string FindPoEDirectory()
		{
			VistaFolderBrowserDialog folderBrowserDialog = new VistaFolderBrowserDialog();
			var result = folderBrowserDialog.ShowDialog();
			if(result.HasValue && result.Value == true)
			{
				return folderBrowserDialog.SelectedPath;
			}
			return string.Empty;
		}

		private TreeViewItem CreateTreeViewItem(string name)
		{
			var item = new TreeViewItem()
			{
				Tag = name,
				FontWeight = FontWeights.Normal,
				Header = name.Split(Path.AltDirectorySeparatorChar)[^1]
			};

			return item;
		}

		private void UpdateTreeViewItemVisibility()
			=> UpdateTreeViewItemVisibility(AssetIndexTree, string.IsNullOrEmpty(SearchText.Text) ? null : new Regex(SearchText.Text, RegexOptions.IgnoreCase));

		private void UpdateTreeViewItemVisibility(ItemsControl item, Regex regex)
		{
			foreach(TreeViewItem child in item.Items)
			{
				if(child != null)
				{
					if(child.Items.Count > 0)
					{
						UpdateTreeViewItemVisibility(child, regex);
					}
					else
					{
						child.Visibility = (regex?.IsMatch(child.Header.ToString()) ?? true) ? Visibility.Visible : Visibility.Collapsed;
					}
				}
			}
		}

		private void PushCursor(Cursor cursor)
		{
			_cursorsStack.Push(Cursor);
			Cursor = cursor;
		}

		private void PopCursor()
		{
			if(_cursorsStack.Count > 0)
			{
				Cursor = _cursorsStack.Pop();
			}
		}

		#endregion

		private void ExportButton_Click(object sender, RoutedEventArgs e)
		{
			if(_openedAssetFile != null)
			{
				SaveFileDialog saveFileDialog = new SaveFileDialog
				{
					Title = $"Export {_openedAssetFile.Name}",
					FileName = Path.GetFileNameWithoutExtension(_openedAssetFile.Name)
				};
				var extension = Path.GetExtension(_openedAssetFile.Name);
				saveFileDialog.Filter = $"Raw file (*{extension})|*{extension}";
				switch(extension)
				{
					case ".dat":
					case ".dat64":
						saveFileDialog.Filter += "|CSV file (*.csv)|*.csv";
						break;
				}
				if(saveFileDialog.ShowDialog() == true && !string.IsNullOrEmpty(saveFileDialog.FileName))
				{
					switch(Path.GetExtension(saveFileDialog.FileName))
					{
						case ".csv":
							StringBuilder sb = new StringBuilder();
							DatFile datFile = new DatFile(_openedAssetFile, _datDefinitions);

							sb.Append("#");
							foreach(var field in datFile.FileDefinition.Fields)
							{
								sb.Append("\t");
								sb.Append(field.ID);
							}

							TryAddColumn("_Remainder");
							TryAddColumn("_RemainderBool");
							TryAddColumn("_RemainderByte");
							TryAddColumn("_RemainderInt");
							TryAddColumn("_RemainderUInt");
							TryAddColumn("_RemainderLong");
							TryAddColumn("_RemainderULong");
							TryAddColumn("_RemainderString");
							TryAddColumn("_RemainderRefString");
							TryAddColumn("_RemainderListULong");
							TryAddColumn("_RemainderListInt");

							void TryAddColumn(string columnName)
							{
								if (datFile.Records.Count > 0 && datFile.Records[0].HasValue(columnName))
								{
									sb.Append("\t");
									sb.Append(columnName);
								}
							}

							sb.AppendLine();

							for (int i = 0; i < datFile.Records.Count; i++)
							{
								var record = datFile.Records[i];

								sb.Append(i);
								foreach(var key in record.Values.Keys)
								{
									sb.Append("\t");
									sb.Append(record.GetStringValue(key));
								}
								sb.AppendLine();
							}
							File.WriteAllText(saveFileDialog.FileName, sb.ToString());
							break;

						default:
							File.WriteAllBytes(saveFileDialog.FileName, _openedAssetFile.GetFileContents());
							break;
					}

					Process.Start("explorer.exe", Path.GetDirectoryName(saveFileDialog.FileName));
				}
			}
		}
	}
}
