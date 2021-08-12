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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace PoEAssetVisualizer
{
	public partial class MainWindow : Window
	{
		#region Consts

		private const string RootNodeName = "ROOT";

		private const string DatDefinitionFileName = "stable.py";

		private static readonly string[] CommonDatFiles = new string[] {
			"BaseItemTypes.dat",
			"Stats.dat",
			"Mods.dat",
			"Tags.dat",
		};

		private static readonly string[] ReferenceFields = new string[]
		{
			"Id",
			"Name",
			"Text",
		};

		private static readonly SolidColorBrush RefColumnColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDEFADE"));

		#endregion

		#region Variables

		private readonly string _poeDirectory;
		private AssetIndex _assetIndex;

		private AssetFile _openedAssetFile;
		private DatDefinitions _datDefinitions;
		private FileSystemWatcher _datDefinitionsWatcher;

		private readonly Dictionary<string, HashSet<string>> _fileDirectories = new Dictionary<string, HashSet<string>>()
		{
			{ RootNodeName, new HashSet<string>() }
		};

		private readonly Stack<Cursor> _cursorsStack = new Stack<Cursor>();

		private readonly Dictionary<string, DatFile> _datFiles = new Dictionary<string, DatFile>();

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

		private void Sub_MouseDoubleClick(object sender, MouseButtonEventArgs e)
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
									_datDefinitionsWatcher = new FileSystemWatcher(Directory.GetCurrentDirectory(), DatDefinitionFileName)
									{
										NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
									};
									_datDefinitionsWatcher.Changed += OnDatDefinitionsFileChanged;
									_datDefinitionsWatcher.EnableRaisingEvents = true;

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
				_datFiles.Clear();
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
			DatViewer.ItemsSource = null;
			DatViewerError.Text = string.Empty;
			DatViewerErrorTab.Visibility = Visibility.Collapsed;
			List<ExpandoObject> items = new List<ExpandoObject>();

			try
			{
				if(_datDefinitions == null)
				{
					_datDefinitions = DatDefinitions.ParseLocalPyPoE(Path.Combine(Directory.GetCurrentDirectory(), DatDefinitionFileName));
				}
				DatFile datFile = GetDatFile(assetFile);

				var firstField = datFile.FileDefinition.Fields.FirstOrDefault();
				DatViewer.FrozenColumnCount = firstField != null && ReferenceFields.Contains(firstField.ID) ? 2 : 1;

				DatViewer.Columns.Add(new DataGridTextColumn()
				{
					Header = "#",
					Binding = new Binding("Index"),
					Width = 40,
				});
				foreach(var field in datFile.FileDefinition.Fields)
				{
					AddColumn(field.ID);
					TryAddRefColumn(field.ID, field.RefDatFileName);
				}
				TryAddColumn("_Remainder");
				TryAddColumn("_RemainderBool");
				TryAddColumn("_RemainderByte");
				TryAddColumn("_RemainderInt");
				TryAddColumn("_RemainderUInt");
				TryAddColumn("_RemainderLong");
				bool hasRemainderULong = TryAddColumn("_RemainderULong");
				if(hasRemainderULong)
				{
					foreach(var commonDatFile in CommonDatFiles)
					{
						TryAddRefColumn("_RemainderULong", commonDatFile);
					}
				}
				TryAddColumn("_RemainderFloat");
				TryAddColumn("_RemainderString");
				TryAddColumn("_RemainderRefString");
				bool hasRemainderListULong = TryAddColumn("_RemainderListULong");
				if(hasRemainderListULong)
				{
					foreach(var commonDatFile in CommonDatFiles)
					{
						TryAddRefColumn("_RemainderListULong", commonDatFile);
					}
				}
				TryAddColumn("_RemainderListInt");

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

					items.Add(row);
				}

				var fieldsWithRefDatFile = datFile.FileDefinition.Fields.Where(x => !string.IsNullOrEmpty(x.RefDatFileName));
				foreach(var field in fieldsWithRefDatFile)
				{
					TryAddRefValues(field.ID, field.DataType.Name, field.RefDatFileName, field.RefDatFieldID);
				}

				if(hasRemainderULong || hasRemainderListULong)
				{
					foreach(var commonDatFile in CommonDatFiles)
					{
						if(hasRemainderULong)
						{
							TryAddRefValues("_RemainderULong", "ulong", commonDatFile, null);
						}
						if(hasRemainderListULong)
						{
							TryAddRefValues("_RemainderListULong", "ref|list|ulong", commonDatFile, null);
						}
					}
				}

				DatViewer.ItemsSource = items;

				ApplyDatViewerFilter();

				DatViewerTab.Visibility = Visibility.Visible;

				// Nested Method(s)
				void AddColumn(string columnName, Brush bgColor = null)
				{
					/*Style style = new Style(typeof(DataGridCell));
					style.Setters.Add(new Setter(ToolTipService.ToolTipProperty, new Binding($"{columnName}_Tooltip")));
					if(bgColor != null)
					{
						style.Setters.Add(new Setter(BackgroundProperty, bgColor));
					}*/

					var column = new DataGridTextColumn()
					{
						Header= columnName.Replace("_", "__"),
						Binding = new Binding(columnName),
						//CellStyle = style, // Styling costs A LOT of performance. Disabled for now.
					};

					DatViewer.Columns.Add(column);
				}

				bool TryAddColumn(string columnName)
				{
					if (datFile.Records.Count > 0 && datFile.Records[0].HasValue(columnName))
					{
						AddColumn(columnName);
						return true;
					}
					return false;
				}

				void TryAddRefColumn(string columnName, string refDatFileName)
				{
					if (!string.IsNullOrEmpty(refDatFileName))
					{
						var refDefintion = _datDefinitions.FileDefinitions.FirstOrDefault(x => x.Name == refDatFileName);
						if (refDefintion != null)
						{
							columnName = $"{columnName}_{Path.GetFileNameWithoutExtension(refDefintion.Name)}";
							foreach (var refFieldName in ReferenceFields)
							{
								var refField = refDefintion.Fields.FirstOrDefault(x => x.ID == refFieldName);
								if (refField != null)
								{
									AddColumn($"{columnName}_{refFieldName}", RefColumnColor);
								}
							}
						}
					}
				}

				void TryAddRefValues(string columnName, string columnDataType, string refDatFileName, string refDatFieldID)
				{
					DatFile refDatFile = GetDatFile($"Data/{refDatFileName}");
					if(refDatFile == null)
					{
						return;
					}
					var columnBaseName = $"{columnName}_{Path.GetFileNameWithoutExtension(refDatFileName)}";
					var refDefintion = refDatFile.FileDefinition;
					var refFields = ReferenceFields.Select(x => refDefintion.Fields.FirstOrDefault(y => y.ID == x)).Where(x => x != null).ToArray();
					if(string.IsNullOrEmpty(refDatFieldID))
					{
						switch(columnDataType)
						{
							case "int":
								AddSingleRefValueByIdx(columnBaseName, x => x.GetValue<int>(columnName));
								break;

							case "ulong":
								AddSingleRefValueByIdx(columnBaseName, x => (int)x.GetValue<ulong>(columnName));
								break;

							case "ref|list|int":
								AddArrayRefValuesByIdx(columnBaseName, x => x.TryGetValue(columnName, out List<int> idxs) ? idxs : null);
								break;

							case "ref|list|ulong":
								AddArrayRefValuesByIdx(columnBaseName, x => x.TryGetValue(columnName, out List<ulong> idxs) ? idxs.Select(x => (int)x).ToList() : null);
								break;

							default:
								throw new Exception($"Unsupported ref data type {columnDataType} (by index)");
						}
					}
					else
					{
						switch(columnDataType)
						{
							case "int":
								AddSingleRefValueByFieldID(columnBaseName, x => x.GetValue<int>(columnName), (a, b) => a == b);
								break;

							case "ulong":
								AddSingleRefValueByFieldID(columnBaseName, x => x.GetValue<ulong>(columnName), (a, b) => a == b);
								break;

							case "ref|string":
								AddSingleRefValueByFieldID(columnBaseName, x => x.GetValue<string>(columnName), (a, b) => a == b);
								break;

							case "ref|list|int":
								AddArrayRefValuesByFieldID(columnBaseName, x => x.TryGetValue(columnName, out List<int> values) ? values : null, (a, b) => a == b);
								break;

							case "ref|list|ulong":
								AddArrayRefValuesByFieldID(columnBaseName, x => x.TryGetValue(columnName, out List<ulong> values) ? values : null, (a, b) => a == b);
								break;

							case "ref|list|ref|string":
								AddArrayRefValuesByFieldID(columnBaseName, x => x.TryGetValue(columnName, out List<string> values) ? values : null, (a, b) => a == b);
								break;

							default:
								throw new Exception($"Unsupported ref data type {columnDataType} (by {refDatFieldID})");
						}
					}

					void AddSingleRefValueByIdx(string columnBaseName, Func<DatRecord, int> getIdx)
					{
						for(int i = 0; i < datFile.Records.Count; i++)
						{
							var record = datFile.Records[i];
							var refValue = getIdx(record);

							if(refValue < 0 || refValue >= refDatFile.Records.Count)
							{
								continue;
							}
							foreach(var refField in refFields)
							{
								var rowDict = (IDictionary<string, object>)items[i];
								rowDict[$"{columnBaseName}_{refField.ID}"] = refDatFile.Records[refValue].GetStringValue(refField.ID);
							}
						}
					}

					void AddSingleRefValueByFieldID<T>(string columnBaseName, Func<DatRecord, T> getRefValue, Func<T, T, bool> matchesRefValue)
					{
						for(int i = 0; i < datFile.Records.Count; i++)
						{
							var record = datFile.Records[i];
							T refValue = getRefValue(record);

							DatRecord refRecord = refDatFile.Records.FirstOrDefault(x => matchesRefValue(x.GetValue<T>(refDatFieldID), refValue));
							if(refRecord == null)
							{
								continue;
							}
							foreach(var refField in refFields)
							{
								var rowDict = (IDictionary<string, object>)items[i];
								rowDict[$"{columnBaseName}_{refField.ID}"] = refRecord.GetStringValue(refField.ID);
							}
						}
					}

					void AddArrayRefValuesByIdx(string columnBaseName, Func<DatRecord, List<int>> getIdxs)
					{
						for(int i = 0; i < datFile.Records.Count; i++)
						{
							var record = datFile.Records[i];
							var idxs = getIdxs(record);
							if(idxs == null || idxs.Any(x => x < 0 || x >= refDatFile.Records.Count))
							{
								continue;
							}
							foreach(var refField in refFields)
							{
								var rowDict = (IDictionary<string, object>)items[i];
								rowDict[$"{columnBaseName}_{refField.ID}"] = string.Concat("[", string.Join(",", idxs.Select(x => refDatFile.Records[x].GetStringValue(refField.ID))), "]");
							}
						}
					}

					void AddArrayRefValuesByFieldID<T>(string columnBaseName, Func<DatRecord, List<T>> getRefValues, Func<T, T, bool> matchesRefValue)
					{
						for(int i = 0; i < datFile.Records.Count; i++)
						{
							var record = datFile.Records[i];
							List<T> refValues = getRefValues(record);
							if(refValues == null)
							{
								continue;
							}

							var refRecords = refValues.Select(x => refDatFile.Records.FirstOrDefault(y => matchesRefValue(y.GetValue<T>(refDatFieldID), x)));

							foreach(var refField in refFields)
							{
								var rowDict = (IDictionary<string, object>)items[i];
								rowDict[$"{columnBaseName}_{refField.ID}"] = string.Concat("[", string.Join(",", refRecords.Select(x => x == null ? string.Empty : x.GetStringValue(refField.ID))), "]");
							}
						}
					}
				}
			}
			catch(Exception ex)
			{
				string message = ex.Message;
				while(ex.InnerException != null)
				{
					message = $"{message} >> {ex.InnerException.Message}";
					ex = ex.InnerException;
				}
				DatViewerError.Text = $"Failed to parse the .dat file ({ex.GetType().FullName}): {message}";
				DatViewerTab.Visibility = Visibility.Collapsed;
				DatViewerErrorTab.Visibility = Visibility.Visible;
			}
		}

		private DatFile GetDatFile(string fileName)
		{
			if(!_datFiles.TryGetValue(fileName, out DatFile datFile))
			{
				return GetDatFile(_assetIndex.FindFile(x => x.Name == fileName));
			}
			return datFile;
		}

		private DatFile GetDatFile(AssetFile assetFile)
		{
			if(assetFile == null)
			{
				return null;
			}
			if(!_datFiles.TryGetValue(assetFile.Name, out DatFile datFile))
			{
				_datFiles[assetFile.Name] = datFile = new DatFile(assetFile, _datDefinitions);
			}
			return datFile;
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
