﻿using CosmosExplorer.Core.Models;
using Microsoft.Azure.Cosmos;

namespace CosmosExplorer.Core.Connection;

public interface IConnectionService
{
    DatabaseProperties? databaseProperties { get; }
    ContainerProperties? containerProperties { get; }
    Database? database { get; }
    Container? container { get; }

    Task<string?> ChangeContainerAsync(string connectionStringOrEndpoint, string databaseName, string containerName);
    Task<IEnumerable<IDatabaseModel>> GetDatabasesAsync(string connectionStringOrEndpoint, CancellationToken cancellationToken);
    
    Partition? Partition { get; }

}

