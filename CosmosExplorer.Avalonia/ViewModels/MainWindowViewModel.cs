﻿using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.Input;
using CosmosExplorer.Avalonia.Services;
using CosmosExplorer.Core;
using CosmosExplorer.Core.Models;
using CosmosExplorer.Core.State;
using DynamicData;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace CosmosExplorer.Avalonia.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly IUserSettingsService userSettingsService;
        private readonly ICosmosDBDocumentService cosmosDbDocumentService;
        private readonly IStateContainer stateContainer;
        private readonly TelemetryService telemetryService;

        private bool isBusy;
        private string errorMessage;
        private string message;
        private string? connectionString;
        private DocumentViewModel? selectedDocument;
        private DatabaseViewModel? selectedDatabase;
        private string? filter;

        private PreferenceConnectionString selectedConnectionString;
        private string? connectionStringName;
        private bool connectionsExpanded;
        private bool addConnectionString;
        private int itemsToRetrieve = 100;
        private readonly int itemsBatch = 100;
        private readonly int maxSavedQueries = 10;

        private bool isFilterExecuted = false;
        private bool isQueryExecuted = false;
        public ObservableCollection<DocumentViewModel> Documents { get; set; } = [];
        public ObservableCollection<DatabaseViewModel> Databases { get; set; } = [];
        public ObservableCollection<PreferenceConnectionString> ConnectionStrings { get; set; } = [];
        public ObservableCollection<string> LastFilters { get; set; } = [];
        public ObservableCollection<string> LastQueries { get; set; } = [];

        private bool hasLastFilters;
        private bool hasLastQueries;
        private decimal scale = 1.0m;
        private string scaleString = "scale(1.0)";
        private decimal change = 0.1m;

        public MainWindowViewModel(IUserSettingsService userSettingsService,
            ICosmosDBDocumentService cosmosDbDocumentService, IStateContainer stateContainer, TelemetryService telemetryService)
        {
            this.userSettingsService =
                userSettingsService ?? throw new ArgumentNullException(nameof(userSettingsService));
            this.cosmosDbDocumentService = cosmosDbDocumentService ??
                                           throw new ArgumentNullException(nameof(cosmosDbDocumentService));
            this.stateContainer = stateContainer ?? throw new ArgumentNullException(nameof(stateContainer));
            this.telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            telemetryService.TrackPageView("MainWindow");
            LoadSettingsAsync();
            QueryBox = new TextDocument("SELECT * FROM c");
        }

        public bool IsBusy
        {
            get => isBusy;
            set => this.RaiseAndSetIfChanged(ref isBusy, value);
        }

        public bool IsFilterExecuted
        {
            get => isFilterExecuted;
            set => this.RaiseAndSetIfChanged(ref isFilterExecuted, value);
        }

        public bool HasLastQueries
        {
            get => hasLastQueries;
            set => this.RaiseAndSetIfChanged(ref hasLastQueries, value);
        }
        public bool HasLastFilters
        {
            get => hasLastFilters;
            set => this.RaiseAndSetIfChanged(ref hasLastFilters, value);
        }

        public bool IsQueryExecuted
        {
            get => isQueryExecuted;
            set => this.RaiseAndSetIfChanged(ref isQueryExecuted, value);
        }

        public string ErrorMessage
        {
            get => errorMessage;
            set => this.RaiseAndSetIfChanged(ref errorMessage, value);
        }

        public string Message
        {
            get => message;
            set => this.RaiseAndSetIfChanged(ref message, value);
        }

        public string? ConnectionString
        {
            get => connectionString;
            set => this.RaiseAndSetIfChanged(ref connectionString, value);
        }

        public string? ConnectionStringName
        {
            get => connectionStringName;
            set => this.RaiseAndSetIfChanged(ref connectionStringName, value);
        }

        public string? Filter
        {
            get { return this.FilterBox.Text; }
            set { this.FilterBox.Text = value; }
        }


        public TextDocument QueryBox { get; set; } = new TextDocument(string.Empty);
        public TextDocument FilterBox { get; set; } = new TextDocument(string.Empty);

        
        public string Query
        {
            get { return this.QueryBox.Text; }
            set { this.QueryBox.Text = value; }
        }

        public DocumentViewModel? SelectedDocument
        {
            get => selectedDocument;
            set
            {
                if (selectedDocument == value)
                    return;
                selectedDocument = value;

                GetDocumentAsync();
            }
        }

        public DatabaseViewModel? SelectedDatabase
        {
            get => selectedDatabase;
            set
            {
                if (selectedDatabase == value)
                    return;
                selectedDatabase = value;
                Documents.Clear();
                FullDocument = "";
                Filter = "";
                Query = "";
                ReloadLastFilters();
                ReloadLastQueries();
                this.RaiseAndSetIfChanged(ref selectedDatabase, value);
            }
        }

        public PreferenceConnectionString SelectedConnectionString
        {
            get => selectedConnectionString;
            set
            {
                if (selectedConnectionString == value || value is null)
                    return;
                selectedConnectionString = value;
                ChangeConnectionStringAsync();
                this.RaisePropertyChanged(nameof(SelectedConnectionString));
            }
        }

        public bool ConnectionsExpanded
        {
            get => connectionsExpanded;
            set => this.RaiseAndSetIfChanged(ref connectionsExpanded, value);
        }

        public bool AddConnectionString
        {
            get => addConnectionString;
            set => this.RaiseAndSetIfChanged(ref addConnectionString, value);
        }

        public TextDocument FullDocumentBox { get; set; } = new TextDocument(string.Empty);

        public string FullDocument
        {
            get { return this.FullDocumentBox.Text; }
            set { this.FullDocumentBox.Text = value; }
        }

        public decimal Scale
        {
            get => scale;
            set
            {
                this.RaiseAndSetIfChanged(ref scale, value);
                if (scale < 0.5m || scale > 2) return;
                ScaleString = $"scale({scale.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)})";
            }
        }

        public string ScaleString
        {
            get => scaleString;
            set => this.RaiseAndSetIfChanged(ref scaleString, value);

        }

       

        [RelayCommand]
        private async Task LoadSettingsAsync()
        {
            var loaded = await userSettingsService.GetSettingsAsync();
            stateContainer.ConnectionStrings = loaded.ConnectionStrings;
            stateContainer.ConnectionString = loaded.ConnectionString;
            AddConnectionString = string.IsNullOrEmpty(stateContainer.ConnectionString);
            if (AddConnectionString) ConnectionsExpanded = true;

            ConnectionStrings = new(this.stateContainer.ConnectionStrings);
            this.RaisePropertyChanged(nameof(ConnectionStrings));

            stateContainer.LastFilters = loaded.LastFilters;
            stateContainer.LastQueries = loaded.LastQueries;
            
            SelectedConnectionString = loaded.ConnectionStrings.FirstOrDefault(c => c.Selected);
        }

        [RelayCommand]
        private async Task AddConnectionStringAsync()
        {
            var addedConnectionString =
                new PreferenceConnectionString(connectionStringName, connectionString, true, false);
            stateContainer.ConnectionStrings.Add(addedConnectionString);
            await userSettingsService.SaveSettingsAsync(stateContainer);
            ConnectionStrings.Add(addedConnectionString);
            ConnectionStringName = ConnectionString = "";
            SelectedConnectionString = addedConnectionString;

            AddConnectionString = false;
        }

        [RelayCommand]
        private async Task DeleteConnectionStringAsync()
        {
            var box = MessageBoxManager
                .GetMessageBoxStandard("Delete?", "Are you sure you want to delete the connection string?",
                    ButtonEnum.YesNo);

            var result = await box.ShowAsync();
            if (result == ButtonResult.Yes)
            {
                var index = stateContainer.ConnectionStrings.IndexOf(selectedConnectionString);
                stateContainer.ConnectionStrings.RemoveAt(index);
                await userSettingsService.SaveSettingsAsync(stateContainer);
                ConnectionStringName = ConnectionString = "";
                await LoadSettingsAsync();
            }
        }

        [RelayCommand]
        private async Task FilterListAsync()
        {
            await FilterAsync();
        }

        private async Task FilterAsync(bool loadmore = false)
        {
            IsBusy = true;
            ErrorMessage = "";

            if (!loadmore)
            {
                IsFilterExecuted = false;
                itemsToRetrieve = itemsBatch;
            }

            try
            {
                await SetContainerAsync();

                (var result, int count, double runits) =
                    (await cosmosDbDocumentService.FilterAsync(Filter, itemsToRetrieve));
                selectedDocument = null;
                Documents.Clear();
                Documents.AddRange(result.Select(x => new DocumentViewModel(x.Item1, x.Item2)));
                AddLastFilter(Filter);
                await userSettingsService.SaveSettingsAsync(stateContainer);
                Message = $"{count} items retrieved, {runits:0.##} RUs";
            }
            catch (Exception e)
            {
                telemetryService.TrackException(e);
                ErrorMessage = e.Message;
                Message = "";
            }

            IsFilterExecuted = true;
            IsBusy = false;
        }

        [RelayCommand]
        private async Task QueryListAsync()
        {
            await QueryAsync();
        }
        
        [RelayCommand]
        private async Task LoadMoreFilterAsync()
        {
            itemsToRetrieve += itemsBatch;
            await FilterAsync(true);
        }

        [RelayCommand]
        private async Task LoadMoreQueryAsync()
        {
            itemsToRetrieve += itemsBatch;
            await QueryAsync(true);
        }
        
        [RelayCommand]
        private async Task NewAsync()
        {
            IsBusy = true;
            ErrorMessage = "";
            try
            {
                await SetContainerAsync();
                Partition partition = cosmosDbDocumentService.Partition;
                StringBuilder newDoc =
                    new StringBuilder(
                        """
                        {
                             "id": "replace_with_new_document_id",
                        """);
                if (partition is not null)
                {
                    newDoc.Append(
                        $"""
                         
                              "{partition.PartitionName1}": "replace_with_new_partition_key_value"
                         """
                    );

                    if (partition.PartitionName2 is not null)
                    {
                        newDoc.Append(
                            $"""
                             ,
                                  "{partition.PartitionName2}": "replace_with_new_partition_key_value"
                             """
                        );
                    }

                    if (partition.PartitionName3 is not null)
                    {
                        newDoc.Append(
                            $"""
                             ,
                                  "{partition.PartitionName3}": "replace_with_new_partition_key_value"
                             """
                        );
                    }

                    newDoc.Append("""

                                  }
                                  """);
                }

                FullDocument = newDoc.ToString();
                selectedDocument = null;
                Documents.Clear();
            }
            catch (Exception e)
            {
                telemetryService.TrackException(e);
                ErrorMessage = e.Message;
                Message = "";
            }

            IsBusy = false;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            IsBusy = true;
            ErrorMessage = "";
            Message = "";
            try
            {
                await SetContainerAsync();

                FullDocument = await cosmosDbDocumentService.UpdateDocumentAsync(selectedDocument?.Id,
                    selectedDocument?.Partition, FullDocument);
            }
            catch (Exception e)
            {
                telemetryService.TrackException(e);
                ErrorMessage = e.Message;
                Message = "";
            }

            Message = (selectedDocument is null) ? "Document is added." : "Document is updated.";
            IsBusy = false;
        }

        [RelayCommand]
        private async Task SetQueryAsync(string item)
        {
            Query = item;
        }
        [RelayCommand]
        private async Task SetFilterAsync(string item)
        {
            Filter = item;
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            ErrorMessage = "";
            Message = "";
            try
            {
                var box = MessageBoxManager
                    .GetMessageBoxStandard("Delete?", "Are you sure you want to delete the document?",
                        ButtonEnum.YesNo);

                var result = await box.ShowAsync();
                if (result == ButtonResult.Yes)
                {
                    IsBusy = true;
                    await cosmosDbDocumentService.DeleteDocumentAsync(selectedDocument.Id, selectedDocument.Partition);
                    await FilterListAsync();
                    Message = "Document is deleted.";
                }
            }
            catch (Exception e)
            {
                telemetryService.TrackException(e);
                ErrorMessage = e.Message;
                Message = "";
            }

            IsBusy = false;
        }

        [RelayCommand]
        private async Task IncreaseZoom()
        {
            Scale += change;
        }
        [RelayCommand]
        private async Task DecreaseZoom()
        {
            Scale -= change;
        }
        [RelayCommand]
        public void OnSliderValueChanged(object newValue)
        {
            // Your logic here
        }
        
        private async Task ReloadAsync()
        {
            IsBusy = true;
            ErrorMessage = "";

            Databases.Clear();
            try
            {
                var databases = await cosmosDbDocumentService.GetDatabasesAsync();

                foreach (var database in databases.OrderBy(x => x.Id))
                {
                    foreach (var container in database.Containers.OrderBy(x => x.Id))
                    {
                        Databases.Add((database.Id, container.Id));
                    }
                }

                if (Databases.Count > 0) SelectedDatabase = Databases[0];
                this.RaisePropertyChanged(nameof(SelectedDatabase));

                ReloadLastQueries();
            }
            catch (Exception e)
            {
                telemetryService.TrackException(e);
                ErrorMessage = e.Message;
            }

            IsBusy = false;
        }

        private async Task QueryAsync(bool loadmore = false)
        {
            IsBusy = true;
            ErrorMessage = "";
            if (!loadmore)
            {
                IsQueryExecuted = false;
                itemsToRetrieve = itemsBatch;
            }

            try
            {
                await SetContainerAsync();

                (var result, int count, double runits) =
                    (await cosmosDbDocumentService.QueryAsync(Query, itemsToRetrieve));
                FullDocument = result;
                selectedDocument = null;
                AddLastQuery(Query);
                await userSettingsService.SaveSettingsAsync(stateContainer);
                Message = $"{count} items retrieved, {runits:0.##} RUs";
            }
            catch (Exception e)
            {
                telemetryService.TrackException(e);
                ErrorMessage = e.Message;
                Message = "";
            }

            IsQueryExecuted = true;
            IsBusy = false;
        }

        private async Task SetContainerAsync()
        {
            await cosmosDbDocumentService.ChangeContainerAsync(selectedDatabase?.Database, selectedDatabase?.Container);
        }
        private void AddLastFilter(string query)
        {
            if (string.IsNullOrEmpty(query)) return;
            var list = stateContainer.LastFilters;
           
            
            var itemToRemove = list.FirstOrDefault(q => q.Query == query && q.Database == stateContainer.Database
                                                                         && q.Container == stateContainer.Container &&
                                                                         q.ConnectionString ==
                                                                         stateContainer.ConnectionString);
            if (itemToRemove is not null) list.Remove(itemToRemove);
            var item = new LastQuery(query, stateContainer.ConnectionString, stateContainer.Database,
                stateContainer.Container);
            list.Insert(0, item);
            if (list.Count > maxSavedQueries) list.RemoveAt(maxSavedQueries);
            stateContainer.LastFilters = list;
            ReloadLastFilters();
        }

        private void AddLastQuery(string query)
        {
            if (string.IsNullOrEmpty(query)) return;
            var list = stateContainer.LastQueries;
           
            var itemToRemove = list.FirstOrDefault(q => q.Query == query && q.Database == stateContainer.Database
                                                                         && q.Container == stateContainer.Container &&
                                                                         q.ConnectionString ==
                                                                         stateContainer.ConnectionString);
            if (itemToRemove is not null) list.Remove(itemToRemove);
            var item = new LastQuery(query, stateContainer.ConnectionString, stateContainer.Database,
                stateContainer.Container);
            list.Insert(0, item);
            if (list.Count > maxSavedQueries) list.RemoveAt(maxSavedQueries);
            stateContainer.LastQueries = list;
            ReloadLastQueries();
        }

        private async Task GetDocumentAsync()
        {
            FullDocument =
                await cosmosDbDocumentService.GetDocumentAsync(selectedDocument?.Id, selectedDocument?.Partition);
        }

        private async Task ChangeConnectionStringAsync()
        {
            if (selectedConnectionString is null) return;

            stateContainer.ConnectionString = selectedConnectionString.ConnectionString;

            SetSelectedConnectionString();
            Documents.Clear();
            FullDocument = "";

            await userSettingsService.SaveSettingsAsync(stateContainer);
            await ReloadAsync();
        }

        private void SetSelectedConnectionString()
        {
            for (int i = 0; i < stateContainer.ConnectionStrings.Count; i++)
            {
                stateContainer.ConnectionStrings[i] = stateContainer.ConnectionStrings[i] with
                {
                    Selected =
                    selectedConnectionString.ConnectionString == stateContainer.ConnectionStrings[i].ConnectionString
                };
            }
        }

        private void ReloadLastFilters()
        {
            if (selectedDatabase is null) return;
            LastFilters.Clear();
            LastFilters.Add(stateContainer.LastFilters.Where(c =>
                c.Database == selectedDatabase.Database
                && c.Container == selectedDatabase.Container
                && c.ConnectionString == SelectedConnectionString.ConnectionString
            ).Select(x => x.Query).ToArray());

            HasLastFilters = LastFilters.Any();
        }

        private void ReloadLastQueries()
        {
            if (selectedDatabase is null) return;
            LastQueries.Clear();
            LastQueries.Add(stateContainer.LastQueries.Where(c =>
                c.Database == selectedDatabase.Database
                && c.Container == selectedDatabase.Container
                && c.ConnectionString == SelectedConnectionString.ConnectionString
            ).Select(x => x.Query).ToArray());

            HasLastQueries = LastQueries.Any();
        }
    }
}