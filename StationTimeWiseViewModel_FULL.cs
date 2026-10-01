using DataChangedNotifier;
using EventsDelegator;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using MigraDoc.DocumentObjectModel;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TIMSDataInsights.CustomDataTypes.DataTypes;
using TIMSDataInsights.CustomDataTypes.Enums;
using TIMSDataInsights.CustomDataTypes.Interfaces;
using TIMSDataInsights.DatabaseAccess;
using TIMSDataInsights.MetroLocationServiceProvider.ViewModels;
using TIMSDataInsights.Resources.Charts;
using AppResourcesInterfaces = AppResources.CustomDataTypes.Interfaces;

namespace TIMSDataInsights.ViewModels
{
    public class StationTimeWiseViewModel : PropertyChangedNotifier
    {
        private AppResourcesInterfaces.IForeignServerDataMapper _foreignServerDataMapper = null;

        private DateTime _FromDateTime = DateTime.Now.Date;

        public DateTime FromDateTime
        {
            get => _FromDateTime;
            set
            {
                RaisePropertyChanged(
                    ref _FromDateTime,
                    value,
                    nameof(FromDateTime));

                UpdateDateRangeDisplay();
            }
        }

        private DateTime _ToDateTime = DateTime.Now.Date;

        public DateTime ToDateTime
        {
            get => _ToDateTime;
            set
            {
                RaisePropertyChanged(
                    ref _ToDateTime,
                    value,
                    nameof(ToDateTime));

                UpdateDateRangeDisplay();
            }
        }

        /// <summary>
        /// Text displayed on the Date/Time range button.
        /// </summary>
        private string _DateRangeDisplayText = string.Empty;

        public string DateRangeDisplayText
        {
            get => _DateRangeDisplayText;
            set
            {
                RaisePropertyChanged(
                    ref _DateRangeDisplayText,
                    value,
                    nameof(DateRangeDisplayText));
            }
        }

        /// <summary>
        /// Updates the date range display text.
        /// </summary>
        private void UpdateDateRangeDisplay()
        {
            try
            {
                if (FromDateTime != null &&
                    ToDateTime != null)
                {
                    string fromDisplay =
                        $"{FromDateTime:MMM dd, yyyy HH:mm:ss}";

                    string toDisplay =
                        $"{ToDateTime:MMM dd, yyyy HH:mm:ss}";

                    DateRangeDisplayText =
                        $"{fromDisplay} - {toDisplay}";
                }
                else
                {
                    DateRangeDisplayText =
                        "Select Date Range";
                }
            }
            catch
            {
                DateRangeDisplayText =
                    "Select Date Range";
            }
        }

        public Action DisplayDateTimeSpanWindow = null;

        public Action StationComboDropDownOpened { get; set; }

        private bool _UplineMode;

        public CommandsHandler DateTimeSpanButtonClickCommandHandler
        {
            get;
            set;
        }

        public CommandsHandler
            StationTimeWiseViewLoadedEventCommandHandler
        {
            get;
            set;
        }

        public bool UplineMode
        {
            get => _UplineMode;

            set
            {
                RaisePropertyChanged(
                    ref _UplineMode,
                    value,
                    nameof(UplineMode));
            }
        }

        private bool _DownlineMode;

        public bool DownlineMode
        {
            get => _DownlineMode;

            set
            {
                RaisePropertyChanged(
                    ref _DownlineMode,
                    value,
                    nameof(DownlineMode));
            }
        }

        private List<string>
            TrainsFilterSelectedItemsBeforeOpeningDropDown = null;

        private List<string>
            StationsFilterSelectedItemsBeforeOpeningDropDown = null;

        private ObservableCollection<string> _Corridors;

        public ObservableCollection<string> Corridors
        {
            get => _Corridors;

            set
            {
                RaisePropertyChanged(
                    ref _Corridors,
                    value,
                    nameof(Corridors));
            }
        }

        private int _CorridorsFilterSelectedIndex;

        public int CorridorsFilterSelectedIndex
        {
            get => _CorridorsFilterSelectedIndex;

            set
            {
                RaisePropertyChanged(
                    ref _CorridorsFilterSelectedIndex,
                    value,
                    nameof(CorridorsFilterSelectedIndex));
            }
        }

        private bool _ShowBusyCursor;

        public bool ShowBusyCursor
        {
            get => _ShowBusyCursor;

            set
            {
                RaisePropertyChanged(
                    ref _ShowBusyCursor,
                    value,
                    nameof(ShowBusyCursor));
            }
        }

        private ISeries[] _Series;

        public ISeries[] Series
        {
            get => _Series;

            set
            {
                RaisePropertyChanged(
                    ref _Series,
                    value,
                    nameof(Series));
            }
        }

        /// <summary>
        /// Optional lookup of series by name.
        /// </summary>
        public Dictionary<string, ISeries> SeriesMap { get; } =
            new Dictionary<string, ISeries>();

        private Axis[] _XAxes;

        public Axis[] XAxes
        {
            get => _XAxes;

            set
            {
                RaisePropertyChanged(
                    ref _XAxes,
                    value,
                    nameof(XAxes));
            }
        }

        private Axis[] _YAxes;

        public Axis[] YAxes
        {
            get => _YAxes;

            set
            {
                RaisePropertyChanged(
                    ref _YAxes,
                    value,
                    nameof(YAxes));
            }
        }

        /// <summary>
        /// All station names. This remains the complete list even when
        /// some stations are not currently selected.
        /// </summary>
        public List<string> AllStations { get; set; } =
            new List<string>();

        /// <summary>
        /// Complete station information returned from the database.
        /// </summary>
        private List<StationAxisItem> AllStationAxisItems { get; set; } =
            new List<StationAxisItem>();

        public CommandsHandler ApplyButtonClickCommandHandler
        {
            get;
            set;
        }

        // ================================================================
        // CACHED DATABASE DATA
        // ================================================================

        /// <summary>
        /// Complete passenger data returned by the database.
        ///
        /// IMPORTANT:
        /// Station selection uses this cached list and does NOT query
        /// the database again.
        /// </summary>
        private List<PassengerStationPoint> _PassengerPoints =
            new List<PassengerStationPoint>();

        /// <summary>
        /// Complete station list returned by the database.
        /// </summary>
        private List<StationAxisItem> _AllStations =
            new List<StationAxisItem>();

        /// <summary>
        /// Prevents checkbox events from rebuilding the chart while the
        /// station selection list is initially being populated.
        /// </summary>
        private bool _IsUpdatingStationSelection = false;

        /// <summary>
        /// Station selection collection displayed by the UI.
        /// </summary>
        private ObservableCollection<StationSelectionItem>
            _StationSelectionItems =
                new ObservableCollection<StationSelectionItem>();

        public ObservableCollection<StationSelectionItem>
            StationSelectionItems
        {
            get => _StationSelectionItems;

            set
            {
                RaisePropertyChanged(
                    ref _StationSelectionItems,
                    value,
                    nameof(StationSelectionItems));
            }
        }

        // ================================================================
        // CONSTRUCTOR
        // ================================================================

        public StationTimeWiseViewModel()
        {
            InitializeFields();
            CommandHandlersRegistration();
        }

        // ================================================================
        // INITIALIZATION
        // ================================================================

        private void InitializeFields()
        {
            ShowBusyCursor = true;

            _foreignServerDataMapper =
                new HMRCForeignServerDataMapper(
                    new SQLDBInteraction());

            _UplineMode = true;
            _DownlineMode = false;

            _Corridors =
                new ObservableCollection<string>();

            // Initialize the station selection collection.
            StationSelectionItems =
                new ObservableCollection<StationSelectionItem>();

            // Start with an empty chart.
            Series = Array.Empty<ISeries>();

            XAxes = Array.Empty<Axis>();

            YAxes = Array.Empty<Axis>();

            ShowBusyCursor = false;
        }

        // ================================================================
        // COMMAND REGISTRATION
        // ================================================================

        private void CommandHandlersRegistration()
        {
            DateTimeSpanButtonClickCommandHandler =
                new CommandsHandler(
                    DateTimeSpanButtonClickEvent,
                    CanExecuteButton);

            ApplyButtonClickCommandHandler =
                new CommandsHandler(
                    ApplyButtonEvent,
                    CanExecuteButton);

            StationTimeWiseViewLoadedEventCommandHandler =
                new CommandsHandler(
                    StationTimeWiseViewLoadedEvent,
                    (object obj) => true);
        }

        private bool CanExecuteButton(object obj)
        {
            return true;
        }

        private void DateTimeSpanButtonClickEvent(
            object eventArgs)
        {
            DisplayDateTimeSpanWindow?.Invoke();
        }

        private void ApplyButtonEvent(object obj)
        {
            // IMPORTANT:
            // Apply Filter is the database trigger.
            //
            // Date/Time, Corridor and Direction are read here.
            // Station checkbox changes never call LoadAsync().
            LoadAsync();
        }

        // ================================================================
        // PAGE LOADED
        // ================================================================

        private async void StationTimeWiseViewLoadedEvent(
            object obj)
        {
            ShowBusyCursor = true;

            try
            {
                // Example/default date range.
                // Replace this if your date range is supplied from another
                // part of your application.
                FromDateTime =
                    new DateTime(
                        2026,
                        6,
                        8,
                        7,
                        0,
                        0);

                ToDateTime =
                    new DateTime(
                        2026,
                        6,
                        8,
                        8,
                        0,
                        0);

                // Load corridor names.
                IList<string> corridors =
                    await Task.Run(
                        () => _foreignServerDataMapper
                            .GetUICorridors());

                Corridors =
                    new ObservableCollection<string>(
                        corridors);

                UplineMode = true;
                DownlineMode = false;

                // Initial page load performs one database load.
                LoadAsync();
            }
            catch (Exception ex)
            {
                // Add your existing application logging here if required.
                System.Diagnostics.Debug.WriteLine(
                    ex);
            }
            finally
            {
                ShowBusyCursor = false;
            }
        }

        // ================================================================
        // DATABASE LOAD
        // ================================================================

        /// <summary>
        /// Loads the complete dataset for the current filters.
        ///
        /// DATABASE CALLS HAPPEN ONLY HERE.
        ///
        /// After this method completes:
        /// - _AllStations contains all stations.
        /// - _PassengerPoints contains all passenger data.
        /// - StationSelectionItems contains all stations.
        /// - First four stations are selected.
        /// - Chart is rendered from the cached data.
        /// </summary>
        public void LoadAsync()
        {
            ShowBusyCursor = true;

            try
            {
                // --------------------------------------------------------
                // 1. READ FILTERS
                // --------------------------------------------------------

                int directionMode =
                    UplineMode ? 0 : 1;

                int corridor =
                    CorridorsFilterSelectedIndex + 1;

                // --------------------------------------------------------
                // 2. GET ALL STATION AXIS DATA
                // --------------------------------------------------------

                DataTable stationAxisTable =
                    _foreignServerDataMapper
                        .GetStationAxisAsync(
                            corridor,
                            directionMode);

                // --------------------------------------------------------
                // 3. GET ALL PASSENGER DATA
                // --------------------------------------------------------

                DataTable passengerDataTable =
                    _foreignServerDataMapper
                        .GetPassengerGraphDataAsync(
                            FromDateTime,
                            ToDateTime,
                            corridor,
                            directionMode);

                // --------------------------------------------------------
                // 4. CONVERT STATION DATA
                // --------------------------------------------------------

                var stations =
                    new List<StationAxisItem>();

                foreach (DataRow row
                         in stationAxisTable.Rows)
                {
                    stations.Add(
                        new StationAxisItem
                        {
                            StationIndex =
                                Convert.ToInt32(
                                    row["StationIndex"]),

                            StationName =
                                row["StationName"]?.ToString()
                                ?? string.Empty
                        });
                }

                // --------------------------------------------------------
                // 5. CONVERT PASSENGER DATA
                // --------------------------------------------------------

                var points =
                    new List<PassengerStationPoint>();

                foreach (DataRow row
                         in passengerDataTable.Rows)
                {
                    points.Add(
                        new PassengerStationPoint
                        {
                            TrainNo =
                                row["TrainNo"]?.ToString()
                                ?? string.Empty,

                            TripId =
                                Convert.ToInt32(
                                    row["TripId"]),

                            StationIndex =
                                Convert.ToInt32(
                                    row["StationIndex"]),

                            CurrentStation =
                                row["CurrentStation"]?.ToString()
                                ?? string.Empty,

                            NextStation =
                                row["NextStation"]?.ToString()
                                ?? string.Empty,

                            DDateTime =
                                Convert.ToDateTime(
                                    row["DDateTime"]),

                            TotalPassenger =
                                Convert.ToInt32(
                                    row["TotalPassenger"])
                        });
                }

                // --------------------------------------------------------
                // 6. CACHE COMPLETE DATABASE RESULT
                // --------------------------------------------------------

                // Keep all stations.
                _AllStations =
                    stations
                        .OrderBy(x => x.StationIndex)
                        .ToList();

                // Keep all passenger points.
                _PassengerPoints = points;

                // Keep the existing public station-name property.
                AllStations =
                    _AllStations
                        .Select(x => x.StationName)
                        .ToList();

                RaisePropertyChanged(
                    nameof(AllStations));

                // --------------------------------------------------------
                // 7. BUILD STATION SELECTION LIST
                // --------------------------------------------------------

                // First four stations are selected by default.
                BuildStationSelectionList(
                    _AllStations);

                // --------------------------------------------------------
                // 8. RENDER THE DEFAULT CHART
                // --------------------------------------------------------

                RefreshStationChart();
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Station chart load cancelled.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Station chart load error: " +
                    ex);
            }
            finally
            {
                ShowBusyCursor = false;
            }
        }

        // ================================================================
        // STATION SELECTION LIST
        // ================================================================

        /// <summary>
        /// Creates the station selection list.
        ///
        /// The first four stations are selected by default.
        ///
        /// This method is called after Apply Filter/database loading.
        /// It is NOT called when the user checks/unchecks a station.
        /// </summary>
        private void BuildStationSelectionList(
            List<StationAxisItem> stations)
        {
            _IsUpdatingStationSelection = true;

            try
            {
                // Clear the old list because a new database dataset has
                // been loaded after Apply Filter.
                StationSelectionItems.Clear();

                var orderedStations =
                    stations
                        .OrderBy(x => x.StationIndex)
                        .ToList();

                for (int i = 0;
                     i < orderedStations.Count;
                     i++)
                {
                    var station =
                        orderedStations[i];

                    var item =
                        new StationSelectionItem
                        {
                            StationIndex =
                                station.StationIndex,

                            StationName =
                                station.StationName,

                            // Default: first four stations.
                            IsSelected = i < 4
                        };

                    // Listen for checkbox changes.
                    item.PropertyChanged +=
                        StationSelectionItem_PropertyChanged;

                    StationSelectionItems.Add(
                        item);
                }
            }
            finally
            {
                _IsUpdatingStationSelection = false;
            }
        }

        // ================================================================
        // STATION SELECTION CHANGE
        // ================================================================

        /// <summary>
        /// Called whenever a station checkbox changes.
        ///
        /// IMPORTANT:
        /// There is NO database call here.
        ///
        /// The chart is rebuilt only from:
        /// _PassengerPoints
        /// _AllStations
        /// </summary>
        private void StationSelectionItem_PropertyChanged(
            object sender,
            PropertyChangedEventArgs e)
        {
            if (e.PropertyName !=
                nameof(
                    StationSelectionItem.IsSelected))
            {
                return;
            }

            // Do not refresh while the initial list is being populated.
            if (_IsUpdatingStationSelection)
            {
                return;
            }

            // Local/in-memory refresh only.
            RefreshStationChart();
        }

        // ================================================================
        // GET SELECTED STATIONS
        // ================================================================

        /// <summary>
        /// Returns stations currently selected by the user.
        ///
        /// No database access.
        /// </summary>
        private List<StationAxisItem>
            GetSelectedStations()
        {
            var selectedIndexes =
                StationSelectionItems
                    .Where(x => x.IsSelected)
                    .Select(x => x.StationIndex)
                    .ToHashSet();

            return _AllStations
                .Where(x =>
                    selectedIndexes.Contains(
                        x.StationIndex))
                .OrderBy(x => x.StationIndex)
                .ToList();
        }

        // ================================================================
        // REFRESH CHART FROM CACHE
        // ================================================================

        /// <summary>
        /// Rebuilds the LiveCharts chart using the cached database data.
        ///
        /// This method does NOT access the database.
        ///
        /// It is called:
        /// 1. After Apply Filter/database load.
        /// 2. When a station is selected.
        /// 3. When a station is unselected.
        /// </summary>
        private void RefreshStationChart()
        {
            if (_AllStations == null ||
                _AllStations.Count == 0)
            {
                Series =
                    Array.Empty<ISeries>();

                XAxes =
                    Array.Empty<Axis>();

                YAxes =
                    Array.Empty<Axis>();

                return;
            }

            // Get only currently selected stations.
            var selectedStations =
                GetSelectedStations();

            // Build X-axis using selected stations.
            BuildAxis(
                selectedStations);

            // Build train/trip series using cached passenger data.
            BuildStackedSeries(
                _PassengerPoints,
                selectedStations);

            // Keep the complete station list for other UI requirements.
            AllStations =
                _AllStations
                    .Select(x => x.StationName)
                    .ToList();

            RaisePropertyChanged(
                nameof(AllStations));
        }

        // ================================================================
        // AXES
        // ================================================================

        /// <summary>
        /// Builds axes for the currently selected stations.
        /// </summary>
        private void BuildAxis(
            List<StationAxisItem> stations)
        {
            var orderedStations =
                stations
                    .OrderBy(x => x.StationIndex)
                    .ToList();

            XAxes =
                new Axis[]
                {
                    new Axis
                    {
                        Name = "Stations",

                        Labels =
                            orderedStations
                                .Select(x =>
                                    x.StationName)
                                .ToArray(),

                        LabelsRotation = 90,

                        TextSize = 10,

                        MinStep = 1,

                        UnitWidth = 1,

                        ForceStepToMin = true,

                        SeparatorsPaint =
                            new SolidColorPaint(
                                new SKColor(
                                    225,
                                    225,
                                    225))
                    }
                };

            YAxes =
                new Axis[]
                {
                    new Axis
                    {
                        Name =
                            "Passenger Count",

                        MinLimit = 0,

                        TextSize = 13,

                        SeparatorsPaint =
                            new SolidColorPaint(
                                new SKColor(
                                    225,
                                    225,
                                    225))
                    }
                };
        }

        // ================================================================
        // STACKED SERIES
        // ================================================================

        /// <summary>
        /// Creates the stacked column chart.
        ///
        /// IMPORTANT CHART STRUCTURE:
        ///
        /// One series = one Train/Trip.
        /// One X position = one selected Station.
        /// Y value = Passenger Count.
        ///
        /// No database call occurs here.
        /// </summary>
        private void BuildStackedSeries(
            List<PassengerStationPoint> points,
            List<StationAxisItem> selectedStations)
        {
            var series =
                new List<ISeries>();

            var stationList =
                selectedStations
                    .OrderBy(x => x.StationIndex)
                    .ToList();

            if (stationList.Count == 0)
            {
                Series =
                    Array.Empty<ISeries>();

                return;
            }

            // Group cached data by Train + Trip.
            var groups =
                points
                    .GroupBy(x => new
                    {
                        x.TrainNo,
                        x.TripId
                    })
                    .OrderBy(x =>
                        x.Min(y => y.DDateTime))
                    .ToList();

            var palette =
                GetPalette();

            int colorIndex = 0;

            foreach (var group in groups)
            {
                string trainNo =
                    group.Key.TrainNo;

                DateTime time =
                    group.Min(
                        x => x.DDateTime);

                // This name is used by the legend.
                // KEEP THIS if the legend should display train/time.
                string seriesName =
                    $"{time:HH:mm} {trainNo}";

                // --------------------------------------------------------
                // FAST STATION LOOKUP
                // --------------------------------------------------------
                //
                // Instead of repeatedly calling:
                //
                // group.FirstOrDefault(...)
                //
                // create a dictionary once.
                //
                var stationLookup =
                    group
                        .GroupBy(
                            x => x.StationIndex)
                        .ToDictionary(
                            g => g.Key,
                            g => g.First());

                var values =
                    new List<
                        PassengerCountStackedPoint>();

                // --------------------------------------------------------
                // BUILD VALUES ONLY FOR SELECTED STATIONS
                // --------------------------------------------------------

                foreach (var station
                         in stationList)
                {
                    if (stationLookup.TryGetValue(
                            station.StationIndex,
                            out var data))
                    {
                        values.Add(
                            new PassengerCountStackedPoint
                            {
                                StationIndex =
                                    station.StationIndex,

                                StationName =
                                    station.StationName,

                                TrainNo =
                                    trainNo,

                                DDateTime =
                                    data.DDateTime,

                                PassengerCount =
                                    data.TotalPassenger
                            });
                    }
                    else
                    {
                        // No passenger record for this train/station.
                        // Add zero so station positions remain aligned.
                        values.Add(
                            new PassengerCountStackedPoint
                            {
                                StationIndex =
                                    station.StationIndex,

                                StationName =
                                    station.StationName,

                                TrainNo =
                                    trainNo,

                                DDateTime =
                                    time,

                                PassengerCount = 0
                            });
                    }
                }

                // --------------------------------------------------------
                // CREATE LIVECHARTS SERIES
                // --------------------------------------------------------

                var columnSeries =
                    new StackedColumnSeries<
                        PassengerCountStackedPoint>
                    {
                        // KEEP Name.
                        // It is required by the legend.
                        Name = seriesName,

                        Values = values,

                        // IMPORTANT:
                        // X is the visible array position.
                        //
                        // This is better than StationIndex because selected
                        // stations can have gaps.
                        //
                        // Example:
                        // StationIndex = 1, 3, 7, 10
                        //
                        // Visible X positions:
                        // 0, 1, 2, 3
                        Mapping = (point, index) =>
                            new Coordinate(
                                index,
                                point.PassengerCount),

                        Fill =
                            new SolidColorPaint(
                                palette[
                                    colorIndex %
                                    palette.Count]),

                        Stroke = null,

                        MaxBarWidth = 100,

                        // White labels work with the darker professional
                        // palette defined below.
                        DataLabelsPaint =
                            new SolidColorPaint(
                                SKColors.White),

                        DataLabelsSize = 11,

                        // ------------------------------------------------
                        // LABEL INSIDE BAR
                        // ------------------------------------------------

                        DataLabelsFormatter = point =>
                        {
                            if (point.Model
                                is PassengerCountStackedPoint data)
                            {
                                if (data.PassengerCount <= 0)
                                {
                                    return string.Empty;
                                }

                                return
                                    $"{data.DDateTime:HH:mm} " +
                                    $"{data.TrainNo} " +
                                    $"{data.PassengerCount}";
                            }

                            return string.Empty;
                        },

                        // ------------------------------------------------
                        // X TOOLTIP
                        // ------------------------------------------------

                        XToolTipLabelFormatter = point =>
                        {
                            if (point.Model
                                is PassengerCountStackedPoint data)
                            {
                                return data.StationName;
                            }

                            return string.Empty;
                        },

                        // ------------------------------------------------
                        // Y TOOLTIP
                        // ------------------------------------------------

                        YToolTipLabelFormatter = point =>
                        {
                            if (point.Model
                                is PassengerCountStackedPoint data)
                            {
                                return
                                    $"Time: " +
                                    $"{data.DDateTime:HH:mm}\n" +
                                    $"Train: " +
                                    $"{data.TrainNo}\n" +
                                    $"Passengers: " +
                                    $"{data.PassengerCount}";
                            }

                            return string.Empty;
                        }
                    };

                series.Add(
                    columnSeries);

                colorIndex++;
            }

            // Replace the chart series in one operation.
            Series =
                series.ToArray();
        }

        // ================================================================
        // PROFESSIONAL COLOR PALETTE
        // ================================================================

        /// <summary>
        /// Professional darker palette for stacked passenger chart.
        ///
        /// Darker colors provide better contrast with the white labels.
        /// The colors are deliberately less saturated than the original
        /// Red/Blue/Green/Orange/Yellow palette.
        /// </summary>
        private List<SKColor> GetPalette()
        {
            return new List<SKColor>
            {
                SKColor.Parse("#2563EB"), // Blue
                SKColor.Parse("#0F766E"), // Teal
                SKColor.Parse("#4338CA"), // Indigo
                SKColor.Parse("#15803D"), // Green
                SKColor.Parse("#7E22CE"), // Purple
                SKColor.Parse("#C2410C"), // Orange
                SKColor.Parse("#BE185D"), // Pink
                SKColor.Parse("#0369A1"), // Ocean Blue
                SKColor.Parse("#6D28D9"), // Violet
                SKColor.Parse("#166534"), // Forest Green

                SKColor.Parse("#B45309"), // Amber
                SKColor.Parse("#9F1239"), // Rose
                SKColor.Parse("#334155"), // Slate
                SKColor.Parse("#1E3A8A"), // Navy
                SKColor.Parse("#115E59"), // Dark Teal

                SKColor.Parse("#7C3AED"), // Purple
                SKColor.Parse("#9A3412"), // Dark Orange
                SKColor.Parse("#881337"), // Dark Pink
                SKColor.Parse("#312E81"), // Dark Indigo
                SKColor.Parse("#164E63"), // Dark Cyan

                SKColor.Parse("#3F3F46"), // Charcoal
                SKColor.Parse("#475569"), // Gray/Slate
                SKColor.Parse("#4C1D95"), // Dark Violet
                SKColor.Parse("#134E4A"), // Deep Teal
                SKColor.Parse("#365314"), // Olive Green
                SKColor.Parse("#78350F"), // Brown
                SKColor.Parse("#7F1D1D"), // Dark Red
                SKColor.Parse("#1E1B4B"), // Deep Indigo
                SKColor.Parse("#1E293B"), // Deep Slate
                SKColor.Parse("#44403C")  // Warm Gray
            };
        }

        // ================================================================
        // CHART FRAME
        // ================================================================

        public DrawMarginFrame
            DrawMarginTimeSeriesFrame =>
            new DrawMarginFrame
            {
                Stroke =
                    new SolidColorPaint(
                        SKColor.Parse(
                            "#D3D3D3"),
                        1)
            };
    }

    // ====================================================================
    // PASSENGER POINT MODEL
    // ====================================================================

    /// <summary>
    /// Custom LiveCharts model.
    ///
    /// StationIndex:
    ///     Original database station index.
    ///
    /// StationName:
    ///     Display name of the station.
    ///
    /// TrainNo:
    ///     Train number used by the legend/labels/tooltip.
    ///
    /// DDateTime:
    ///     Train/passenger timestamp.
    ///
    /// PassengerCount:
    ///     Y-axis value.
    /// </summary>
    public class PassengerCountStackedPoint
    {
        public int StationIndex { get; set; }

        public string StationName { get; set; }

        public string TrainNo { get; set; }

        public DateTime DDateTime { get; set; }

        public double PassengerCount { get; set; }
    }

    // ====================================================================
    // STATION SELECTION MODEL
    // ====================================================================

    /// <summary>
    /// Represents one station in the station-selection ListBox.
    ///
    /// IsSelected:
    ///     true  -> station is rendered.
    ///     false -> station is not rendered.
    ///
    /// Changing IsSelected does not access the database.
    /// </summary>
    public class StationSelectionItem :
        INotifyPropertyChanged
    {
        private bool _IsSelected;

        public int StationIndex { get; set; }

        public string StationName { get; set; }

        public bool IsSelected
        {
            get => _IsSelected;

            set
            {
                if (_IsSelected == value)
                {
                    return;
                }

                _IsSelected = value;

                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(
                        nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler
            PropertyChanged;
    }
}
