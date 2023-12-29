﻿using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CosmosExplorer.Core;
using CosmosExplorer.Core.Models;
using CosmosExplorer.Core.State;
using DynamicData;

namespace CosmosExplorer.Avalonia.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly IUserSettingsService userSettingsService;
        private readonly ICosmosDBDocumentService cosmosDbDocumentService;
        private IStateContainer stateContainer;
        public ICommand LoadSettingsAsyncCommand { get; }
        public ICommand SaveSettingsAsyncCommand { get; }
        public ICommand QueryAsyncCommand { get; }
        public ICommand GetDocumentAsyncCommand { get; }
        public ICommand ChangeConnectionStringCommand { get; }

        public ObservableCollection<string> FullDocuments { get; } = new ();
        public ObservableCollection<(string, string)> Documents { get; set; } = new ();
        public ObservableCollection<(string database, string container)> Databases { get; set; } = new ();


        public ObservableCollection<PreferenceConnectionString> ConnectionStrings { get; set; }
        
        private string? connectionString;
        public string? ConnectionString
        {
            get => connectionString;
            set => this.RaiseAndSetIfChanged(ref connectionString, value);
        }
        
        private string? connectionStringName;
        public string? ConnectionStringName
        {
            get => connectionStringName;
            set => this.RaiseAndSetIfChanged(ref connectionStringName, value);
        }
        
        private string? query;
        public string? Query
        {
            get => query;
            set => this.RaiseAndSetIfChanged(ref query, value);
        }
        
       
        private (string,string)? selectedDocument;
        public (string,string)? SelectedDocument
        {
            get { return selectedDocument; }
            set
            {
                if (selectedDocument == value)
                    return;
                selectedDocument = value;
                
                GetDocumentAsyncCommand.Execute(null);
            }
        }
        
       
        private (string database, string container)? selectedDatabase;
        public (string database, string container)? SelectedDatabase
        {
            get { return selectedDatabase; }
            set
            {
                if (selectedDatabase == value)
                    return;
                selectedDatabase = value;
                GetDocumentAsyncCommand.Execute(null);
            }
        }
        
        private PreferenceConnectionString selectedConnectionString;
        public PreferenceConnectionString SelectedConnectionString
        {
            get { return selectedConnectionString; }
            set
            {
                if (selectedConnectionString == value)
                    return;
                selectedConnectionString = value;
                ChangeConnectionStringCommand.Execute(null);
            }
        }
        
        public MainWindowViewModel(IUserSettingsService userSettingsService, ICosmosDBDocumentService cosmosDbDocumentService,IStateContainer stateContainer )
        {
            this.userSettingsService = userSettingsService ?? throw new ArgumentNullException(nameof(userSettingsService));
            this.cosmosDbDocumentService = cosmosDbDocumentService ?? throw new ArgumentNullException(nameof(cosmosDbDocumentService));
            this.stateContainer = stateContainer ?? throw new ArgumentNullException(nameof(stateContainer));
            
            LoadSettingsAsyncCommand = ReactiveCommand.CreateFromTask(LoadSettingsAsync);
            SaveSettingsAsyncCommand = ReactiveCommand.CreateFromTask(SaveSettingsAsync);
            QueryAsyncCommand = ReactiveCommand.CreateFromTask(QueryAsync); 
            GetDocumentAsyncCommand = ReactiveCommand.CreateFromTask(GetDocumentAsync);
            ChangeConnectionStringCommand = ReactiveCommand.CreateFromTask(ChangeConnectionStringAsync); 
   
            LoadSettingsAsyncCommand.Execute(null);
            
        }

        private async Task LoadSettingsAsync()
        {
            var loaded = await userSettingsService.GetSettingsAsync();
            stateContainer.ConnectionStrings = loaded.ConnectionStrings;
            stateContainer.ConnectionString = loaded.ConnectionString;

            ConnectionStrings = new (this.stateContainer.ConnectionStrings);
          
            await ReloadAsync();
        }

        private async Task ReloadAsync()
        {
            Databases.Clear();
            var databases = await cosmosDbDocumentService.GetDatabasesAsync();
            SelectedDatabase = null;
            
            foreach (var database in databases.OrderBy(x=>x.Id))
            {
                
                foreach (var container in database.Containers.OrderBy(x=>x.Id))
                {
                    Databases.Add((database.Id, container.Id));
                }
            }

            SelectedDatabase = Databases[0];
        }

        private async Task SaveSettingsAsync()
        {
            var addConnectionString = new PreferenceConnectionString(connectionStringName, connectionString, true);
            stateContainer.ConnectionStrings.Add(addConnectionString);
            await userSettingsService.SaveSettingsAsync(stateContainer);
            ConnectionStrings.Add(addConnectionString);
            connectionStringName = connectionString = "";
        }
        
        private async Task QueryAsync()
        {
            try
            {
                await cosmosDbDocumentService.ChangeContainerAsync(selectedDatabase?.database, selectedDatabase?.container);
                
                (var result, int count) = (await cosmosDbDocumentService.QueryAsync(query, 100));
                selectedDocument = null;
                Documents.Clear();
                Documents.AddRange(result.ToArray());

            }
            catch (Exception e)
            {
                
            }
        }

        private async Task GetDocumentAsync()
        {
            var doc = await cosmosDbDocumentService.GetDocumentAsync(selectedDocument?.Item1,selectedDocument?.Item2);
            FullDocuments.Clear();
            FullDocuments.Add(doc);
        }
        
        private async Task ChangeConnectionStringAsync()
        {
            if (selectedConnectionString is null) return;
            
            stateContainer.ConnectionString = selectedConnectionString.ConnectionString;
            await userSettingsService.SaveSettingsAsync(stateContainer);
            await ReloadAsync();
        }
    }
}
