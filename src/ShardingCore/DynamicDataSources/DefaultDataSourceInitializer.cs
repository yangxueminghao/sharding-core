﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShardingCore.Core.EntityMetadatas;
using ShardingCore.Core.ShardingConfigurations.Abstractions;
using ShardingCore.Core.VirtualDatabase.VirtualDataSources;
using ShardingCore.Core.VirtualRoutes.TableRoutes.RouteTails.Abstractions;
using ShardingCore.TableCreator;
using System;
using System.Collections.Generic;
using System.Threading;
using ShardingCore.Core.DbContextCreator;
using ShardingCore.Core.ServiceProviders;
using ShardingCore.Core.ShardingConfigurations;
using ShardingCore.Core.VirtualRoutes.Abstractions;
using ShardingCore.Core.VirtualRoutes.TableRoutes;
using ShardingCore.Exceptions;
using ShardingCore.Extensions;
using ShardingCore.Sharding.Abstractions;
using ShardingCore.TableExists.Abstractions;

namespace ShardingCore.DynamicDataSources
{
    public class DataSourceInitializer : IDataSourceInitializer
    {
        private readonly ILogger<DataSourceInitializer> _logger;

        private readonly IShardingProvider _shardingProvider;
        private readonly IDbContextCreator _dbContextCreator;
        private readonly ShardingConfigOptions _shardingConfigOptions;
        private readonly IVirtualDataSource _virtualDataSource;
        private readonly IRouteTailFactory _routeTailFactory;
        private readonly IDataSourceRouteManager _dataSourceRouteManager;
        private readonly ITableRouteManager _tableRouteManager;
        private readonly IEntityMetadataManager _entityMetadataManager;
        private readonly IShardingTableCreator _tableCreator;
        private readonly ITableEnsureManager _tableEnsureManager;

        public DataSourceInitializer(
            IShardingProvider shardingProvider,
            IDbContextCreator dbContextCreator,
            ShardingConfigOptions shardingConfigOptions,
            IVirtualDataSource virtualDataSource,
            IRouteTailFactory routeTailFactory,
            IDataSourceRouteManager dataSourceRouteManager,
            ITableRouteManager tableRouteManager,
            IEntityMetadataManager entityMetadataManager,
            IShardingTableCreator shardingTableCreator,
            ITableEnsureManager tableEnsureManager,
            ILogger<DataSourceInitializer> logger)
        {
            _shardingProvider = shardingProvider;
            _dbContextCreator = dbContextCreator;
            _shardingConfigOptions = shardingConfigOptions;
            _virtualDataSource = virtualDataSource;
            _routeTailFactory = routeTailFactory;
            _dataSourceRouteManager = dataSourceRouteManager;
            _tableRouteManager = tableRouteManager;
            _entityMetadataManager = entityMetadataManager;
            _tableCreator = shardingTableCreator;
            _tableEnsureManager = tableEnsureManager;
            _logger = logger;
        }

        public void InitConfigure(string dataSourceName, bool createDatabase, bool createTable)
        {
            using (var shardingScope = _shardingProvider.CreateScope())
            {
                using (var shellDbContext = _dbContextCreator.GetShellDbContext(shardingScope.ServiceProvider))
                {
                    var isDefault = _virtualDataSource.IsDefault(dataSourceName);
                    if (createDatabase)
                    {
                        EnsureCreated(isDefault, shellDbContext, dataSourceName);
                    }

                    if (createTable)
                    {
                        var existTables =
                            _tableEnsureManager.GetExistTables((IShardingDbContext)shellDbContext, dataSourceName);
                        var allShardingEntities = _entityMetadataManager.GetAllShardingEntities();
                        foreach (var entityType in allShardingEntities)
                        {
                            var entityMetadata = _entityMetadataManager.TryGet(entityType);
                            if (entityMetadata.IsShardingDataSource())
                            {
                                var virtualDataSourceRoute = _dataSourceRouteManager.GetRoute(entityType);
                                //如果是分库对象就要判断是否含有当前数据源
                                if (virtualDataSourceRoute.GetAllDataSourceNames().Contains(dataSourceName))
                                {
                                    CreateDataTable(dataSourceName, entityMetadata, existTables);
                                }
                            }
                            //不是分库对象
                            else
                            {
                                if (_virtualDataSource.IsDefault(dataSourceName))
                                {
                                    CreateDataTable(dataSourceName, entityMetadata, existTables);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void EnsureCreated(bool isDefault, DbContext context,
            string dataSourceName)
        {
            if (context is IShardingDbContext shardingDbContext)
            {
                using (var dbContext =
                       shardingDbContext.GetIndependentWriteDbContext(dataSourceName,
                           _routeTailFactory.Create(string.Empty, false)))
                {
                    if (isDefault)
                    {
                        dbContext.RemoveDbContextRelationModelThatIsShardingTable();
                    }
                    else
                    {
                        dbContext.RemoveDbContextAllRelationModelWithoutShardingDataSourceOnly();
                    }

                    dbContext.Database.EnsureCreated();
                }
            }
            else
            {
                throw new ShardingCoreInvalidOperationException(
                    $"{nameof(IDbContextCreator)}.{nameof(IDbContextCreator.GetShellDbContext)} db context type not impl {nameof(IShardingDbContext)}");
            }
        }

        // private void CreateDataTable(string dataSourceName, IVirtualTableRoute tableRoute, ISet<string> existTables)
        private void CreateDataTable(string dataSourceName, EntityMetadata entityMetadata, ISet<string> existTables)
        {
            if (!entityMetadata.IsShardingTable())
            {
                var physicTableName = $"{entityMetadata.LogicTableName}";
                try
                {
                    //添加物理表
                    if (!existTables.Contains(physicTableName))
                        _tableCreator.CreateTable(dataSourceName, entityMetadata.EntityType, string.Empty);
                }
                catch (Exception e)
                {
                    if (!_shardingConfigOptions.IgnoreCreateTableError.GetValueOrDefault())
                    {
                        _logger.LogWarning(e,
                            $"table :{physicTableName} will created.");
                    }
                }
            }
            else
            {
                var tableRoute = _tableRouteManager.GetRoute(entityMetadata.EntityType);
                foreach (var tail in tableRoute.GetTails())
                {
                    var physicTableName = $"{entityMetadata.LogicTableName}{entityMetadata.TableSeparator}{tail}";
                    try
                    {
                        //添加物理表
                        if (!existTables.Contains(physicTableName))
                            _tableCreator.CreateTable(dataSourceName, entityMetadata.EntityType, tail);
                    }
                    catch (Exception e)
                    {
                        if (!_shardingConfigOptions.IgnoreCreateTableError.GetValueOrDefault())
                        {
                            _logger.LogWarning(e,
                                $"table :{physicTableName} will created.");
                        }
                    }
                }
            }
        }
    }
}