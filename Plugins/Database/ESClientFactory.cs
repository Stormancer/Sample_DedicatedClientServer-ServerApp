using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nest;
using Stormancer;
using Stormancer.Plugins;
using Stormancer.Server.Components;
using System.Collections.Concurrent;

namespace Server.Database
{
    public class Startup
    {
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new ESClientPlugin());

        }
    }

    internal class ESClientPlugin : IHostPlugin
    {
        private object synclock = new object();

        public void Build(HostPluginBuildContext ctx)
        {

            ctx.HostDependenciesRegistration += (IDependencyBuilder b) =>
            {
                b.Register<ESClientFactory>().As<IESClientFactory>().SingleInstance();
            };

        }
    }
    public interface IESClientFactory
    {
        Task<Nest.IElasticClient> CreateClient(string index);

    }
    class ESClientFactory : IESClientFactory, IDisposable
    {
        private IEnvironment _environment;
        private AsyncLock _lock = new AsyncLock();
        private IEnumerable<Stormancer.Server.Index> _indices;
        private ConcurrentDictionary<string, Nest.ElasticClient> _client = new ConcurrentDictionary<string, ElasticClient>();
        private Elasticsearch.Net.IConnectionPool _connectionPool = new Elasticsearch.Net.StaticConnectionPool(new[] { new Uri("http://localhost.fiddler:9200") });

        //private List<Elasticsearch.Net.Connection.HttpClientConnection> _connections = new List<Elasticsearch.Net.Connection.HttpClientConnection>();
        public ESClientFactory(IEnvironment environment)
        {
            _environment = environment;

        }

        public async Task<IElasticClient> CreateClient(string indexName)
        {
            if (_indices == null || !_indices.Any(i => i.name == indexName))
            {
                using (await _lock.LockAsync())
                {
                    if (_indices == null || !_indices.Any(i => i.name == indexName))
                    {
                        _indices = await _environment.ListIndices();
                    }
                }
            }
            //var endpoint = (await _environment.GetApplicationInfos()).ApiEndpoint;
            var index = _indices.FirstOrDefault(i => i.name == indexName);
            if (index != null)
            {
                indexName = index.accountId + "-" + index.name;
            }
            return _client.GetOrAdd(indexName, i =>
            {


                //var connection = new Elasticsearch.Net.Connection.HttpClientConnection(
                //     new ConnectionSettings(),
                //     new AuthenticatedHttpClientHandler(index));
                //_connections.Add(connection);
                return new Nest.ElasticClient(new ConnectionSettings(_connectionPool).DefaultIndex(indexName.ToLowerInvariant()).MaximumRetries(10).MaxRetryTimeout(TimeSpan.FromSeconds(30)));
            });

        }

        public void Dispose()
        {
            //foreach (var c in _connections)
            //{
            //    c.Dispose();
            //}
        }
    }
}
