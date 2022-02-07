using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using TodoApi.Models;
using Azure.Identity;
using Azure.Core;
using StackExchange.Redis;
using System.Threading;
using System.Text;
using System.Net.Sockets;

namespace TodoApi.Controllers
{
    [Route("api/[controller]")] 
    [ApiController]
    public class TodoController : Controller
    {
        private readonly TodoContext _context;
        private const string SecretName = "AzCertRedisDemo.redis.cache.windows.net:6380,password=MMnylLwsQbDOIWfc1CKaonsvoaANwQ3ENAzCaPNqrgU=,ssl=True,abortConnect=False";
        private static long _lastReconnectTicks = DateTimeOffset.MinValue.UtcTicks;
        private static DateTimeOffset _firstErrorTime = DateTimeOffset.MinValue;
        private static DateTimeOffset _previousErrorTime = DateTimeOffset.MinValue;
        private static SemaphoreSlim _reconnectSemaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1);
        private static SemaphoreSlim _initSemaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1);
        private static ConnectionMultiplexer _connection = ConnectionMultiplexer.ConnectAsync(SecretName).GetAwaiter().GetResult();
        private static bool _didInitialize = false;
        public TodoController(TodoContext context)
        {
            _context = context;

            if (_context.TodoItems.Count() == 0)
            {
                _context.TodoItems.Add(new TodoItem { Name = "Item1" });
                _context.SaveChanges();
            }
        }

        // GET: api/Todo
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TodoItem>>> GetTodoItem()
        {
            //GetRedisValues();
            SecretClientOptions options = new SecretClientOptions()
            {
                Retry =
        {
            Delay= TimeSpan.FromSeconds(2),
            MaxDelay = TimeSpan.FromSeconds(16),
            MaxRetries = 5,
            Mode = RetryMode.Exponential
         }
            };
            var client = new SecretClient(new Uri("https://azcertkeyvaultdemo.vault.azure.net/"), new DefaultAzureCredential(), options);

            KeyVaultSecret secret = client.GetSecret("CertDemoVault");

            string secretValue = secret.Value;
            return await _context.TodoItems.ToListAsync();
        }

        private void GetRedisValues()
        {
            ViewBag.Message = "A simple example with Azure Cache for Redis on ASP.NET Core.";
            IDatabase cache = _connection.GetDatabase();

            // Perform cache operations using the cache object...
            // Simple PING command
            ViewBag.command1 = "PING";
            ViewBag.command1Result = cache.Execute(ViewBag.command1).ToString();
            // Simple get and put of integral data types into the cache
            ViewBag.command2 = "GET Message";
            ViewBag.command2Result = cache.StringGet("Message").ToString();
            ViewBag.command3 = "SET Message \"Hello! The cache is working from ASP.NET Core!\"";
            ViewBag.command3Result = cache.StringSet("Message", "Hello! The cache is working from ASP.NET Core!").ToString();
            // Demonstrate "SET Message" executed as expected...
            ViewBag.command4 = "GET Message";
            ViewBag.command4Result = cache.StringGet("Message").ToString();
            // Get the client list, useful to see if connection list is growing...
            // Note that this requires allowAdmin=true in the connection string
            ViewBag.command5 = "CLIENT LIST";
            StringBuilder sb = new StringBuilder();

            var endpoint = (System.Net.DnsEndPoint)_connection.GetEndPoints()[0];
            IServer server = _connection.GetServer(endpoint.Host, endpoint.Port);
            ClientInfo[] clients = server.ClientList();
            sb.AppendLine("Cache response :");
            foreach (ClientInfo client in clients)
            {
                sb.AppendLine(client.Raw);
            }
            ViewBag.command5Result = sb.ToString();
        }

        // GET: api/Todo/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TodoItem>> GetTodoItem(long id)
        {
            var todoItem = await _context.TodoItems.FindAsync(id);

            if (todoItem == null)
            {
                return NotFound();
            }

            return todoItem;
        }

        // PUT: api/Todo/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTodoItem(long id, TodoItem todoItem)
        {
            if (id != todoItem.Id)
            {
                return BadRequest();
            }

            _context.Entry(todoItem).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TodoItemExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Todo
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPost]
        public async Task<ActionResult<TodoItem>> PostTodoItem(TodoItem todoItem)
        {
            _context.TodoItems.Add(todoItem);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetTodoItem", new { id = todoItem.Id }, todoItem);
        }

        // DELETE: api/Todo/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<TodoItem>> DeleteTodoItem(long id)
        {
            var todoItem = await _context.TodoItems.FindAsync(id);
            if (todoItem == null)
            {
                return NotFound();
            }

            _context.TodoItems.Remove(todoItem);
            await _context.SaveChangesAsync();

            return todoItem;
        }

        private bool TodoItemExists(long id)
        {
            return _context.TodoItems.Any(e => e.Id == id);
        }
        
    }
}
