﻿using System.Collections.Concurrent;
using MySqlConnector;
using ShardingCore.Core.EntityMetadatas;
using ShardingCore.Core.VirtualDatabase.VirtualDataSources;
using ShardingCore.Core.VirtualRoutes;
using ShardingCore.Core.VirtualRoutes.DataSourceRoutes.RouteRuleEngine;
using ShardingCore.Core.VirtualRoutes.TableRoutes.Abstractions;
using ShardingCore.TableCreator;

namespace Sample.AutoCreateIfPresent
{
    public class AreaDeviceRoute : AbstractShardingOperatorVirtualTableRoute<AreaDevice, string>
    {
        private readonly IVirtualDataSource _virtualDataSource;
        private readonly IShardingTableCreator _tableCreator;
        private const string Tables = "Tables";
        private const string TABLE_SCHEMA = "TABLE_SCHEMA";
        private const string TABLE_NAME = "TABLE_NAME";

        private const string CurrentTableName = nameof(AreaDevice);

        private readonly ConcurrentDictionary<string, object?> _tails =
            new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        private readonly object _lock = new object();
        private readonly object _initLock = new object();
        private bool _inited = false;

        public AreaDeviceRoute(IVirtualDataSource virtualDataSource, IShardingTableCreator tableCreator)
        {
            _virtualDataSource = virtualDataSource;
            _tableCreator = tableCreator;
        }

        private void InitTails()
        {
            using (var connection = new MySqlConnection(_virtualDataSource.DefaultConnectionString))
            {
                connection.Open();
                var database = connection.Database;

                using (var dataTable = connection.GetSchema(Tables))
                {
                    for (int i = 0; i < dataTable.Rows.Count; i++)
                    {
                        var schema = dataTable.Rows[i][TABLE_SCHEMA];
                        if (database.Equals($"{schema}", StringComparison.OrdinalIgnoreCase))
                        {
                            var tableName = dataTable.Rows[i][TABLE_NAME]?.ToString() ?? string.Empty;
                            if (tableName.StartsWith(CurrentTableName, StringComparison.OrdinalIgnoreCase))
                            {
                                //如果没有下划线那么需要CurrentTableName.Length有下划线就要CurrentTableName.Length+1
                                _tails.TryAdd(tableName.Substring(CurrentTableName.Length + 1), null);
                            }
                        }
                    }
                }
            }
        }


        public override string ShardingKeyToTail(object shardingKey)
        {
            return $"{shardingKey}";
        }

        /// <summary>
        /// 如果你是非mysql数据库请自行实现这个方法返回当前类在数据库已经存在的后缀
        /// 仅启动时调用
        /// </summary>
        /// <returns></returns>
        public override List<string> GetTails()
        {
            if (!_inited)
            {
                lock(_initLock)
                {
                    if (!_inited)
                    {
                        InitTails();
                        _inited = true;
                    }
                }
            }

            return _tails.Keys.ToList();
        }

        public override void Configure(EntityMetadataTableBuilder<AreaDevice> builder)
        {
            builder.ShardingProperty(o => o.Area);
            builder.TableSeparator(string.Empty);
        }

        public override Func<string, bool> GetRouteToFilter(string shardingKey, ShardingOperatorEnum shardingOperator)
        {
            var t = ShardingKeyToTail(shardingKey);
            switch (shardingOperator)
            {
                case ShardingOperatorEnum.Equal: return tail => tail == t;
                default:
                {
#if DEBUG
                    Console.WriteLine($"shardingOperator is not equal scan all table tail");
#endif
                    return tail => true;
                }
            }
        }

        public override TableRouteUnit RouteWithValue(DataSourceRouteResult dataSourceRouteResult, object shardingKey)
        {
            var shardingKeyToTail = ShardingKeyToTail(shardingKey);
            if (!_tails.TryGetValue(shardingKeyToTail, out var _))
            {
                lock (_lock)
                {
                    if (!_tails.TryGetValue(shardingKeyToTail, out var _))
                    {
                        try
                        {
                            _tableCreator.CreateTable<AreaDevice>(_virtualDataSource.DefaultDataSourceName,
                                shardingKeyToTail);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("尝试添加表失败" + ex);
                        }

                        _tails.TryAdd(shardingKeyToTail, null);
                    }
                }
            }

            return base.RouteWithValue(dataSourceRouteResult, shardingKey);
        }
    }
}