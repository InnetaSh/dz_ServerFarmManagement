
using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;
using static System.Runtime.InteropServices.JavaScript.JSType;



//Тебе необходимо разработать систему для управления фермой серверов, которая обрабатывает запросы от клиентов, выполняет задачи резервного копирования и анализ данных. В этой системе есть несколько типов задач, которые необходимо выполнять асинхронно и управлять их выполнением с учётом ограниченных ресурсов сервера. Система должна поддерживать приоритеты задач, контроль исключений и отмену долгих операций.

//Условия:
//У тебя есть ферма серверов из 5 машин. Каждый сервер может одновременно выполнять не более 3 задач.

//Три типа задач, которые выполняются на серверах:

//Обработка клиентских запросов (ProcessClientRequestsAsync): задачи низкого приоритета.
//Анализ данных (DataAnalysisAsync): задачи среднего приоритета.
//Резервное копирование (BackupAsync): задачи высокого приоритета.
//Класс Server:

//Каждый сервер должен иметь ограничение на количество одновременно выполняемых задач (максимум 3).
//Если на сервере нет свободных слотов, задачи должны быть в очереди, и их выполнение должно начаться, как только освободится слот.
//Для каждой задачи создавай случайную задержку (например, от 3 до 10 секунд).
//Управление задачами:

//Задачи должны запускаться параллельно на разных серверах, но с учетом приоритета:
//Резервное копирование(высокий приоритет) должно выполняться как можно скорее.
//Анализ данных (средний приоритет) должен выполняться быстрее, чем задачи обработки клиентских запросов.
//Обработка клиентских запросов (низкий приоритет) может выполняться в любое время.
//Обработка исключений:

//Любая задача может завершиться с ошибкой. Ошибки необходимо обрабатывать асинхронно, и выполнение остальных задач не должно останавливаться.
//После ошибки в задаче система должна логировать ошибку, но продолжать выполнение других задач.
//Отмена задач:

//Система должна позволять отменять задачи через CancellationToken, если задачи не были завершены за определённое время (например, 30 секунд).
//Если задача была отменена, она должна логироваться как "отмененная".
//Контроль ресурсов:

//Тебе нужно использовать SemaphoreSlim для контроля количества одновременно выполняющихся задач на каждом сервере.
//Задачи высокого приоритета могут вытеснять задачи низкого приоритета, если все слоты на серверах заняты (задачи низкого приоритета должны быть отменены или приостановлены).


class Program
{
    static async Task Main(string[] args)
    {
        var servers = new List<Server>
        {
            new Server(),
            new Server(),
            new Server(),
            new Server(),
            new Server()
        };

        var taskManager = new TaskManager(servers);
        CancellationTokenSource cancelTokenSource = new CancellationTokenSource();


        cancelTokenSource.CancelAfter(TimeSpan.FromSeconds(10));
        Task task = null;
       
                Random random = new Random();
                for (var i = 0; i < 25; i++)
                {
                    taskManager.AddTask(new TaskFunctions(random.Next(1, 4)));
                }
      
            task = taskManager.StartProcessingAsync(cancelTokenSource.Token);
      
            try
            {
                await Task.WhenAll(task);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Произошла ошибка в StartProcessing: {ex.Message}");
            }
            finally
            {
                cancelTokenSource.Dispose();

            }

        Console.ReadKey();
    }
}



public class Server
{
    internal readonly SemaphoreSlim Semaphore = new SemaphoreSlim(3);

    public async Task ExecuteTaskAsync(CancellationToken token, TaskFunctions func)
    {
            try
            {
                await Semaphore.WaitAsync();
                await func.Process(token);
            }
            catch (Exception ex) {  Console.WriteLine(ex.Message); }
            finally { Semaphore.Release(); }
        
    }

}

public class TaskManager
{
    private readonly List<Server> _servers;
    private readonly List<TaskFunctions> _funcs = new List<TaskFunctions>();

    public TaskManager(List<Server> servers)
    {
        _servers = servers;
    }

    public void AddTask(TaskFunctions func)
    {
        _funcs.Add(func);
    }

    public async Task StartProcessingAsync(CancellationToken token)
    {
        var _orderFunc = _funcs.OrderBy(x => x.Priority).ToList();

        var tasks = new List<Task>();

        while (_orderFunc.Count > 0)
        {
            if (token.IsCancellationRequested)
            {
                token.ThrowIfCancellationRequested();
                break;
            }

            var servIndex = 0;

            while (servIndex < _servers.Count)
            {
                if (_servers[servIndex].Semaphore.CurrentCount > 0)
                {
                    var func = _orderFunc[0];
                    _orderFunc.RemoveAt(0);
                   
                    tasks.Add(_servers[servIndex].ExecuteTaskAsync(token, func));

                    break;
                }

                servIndex++;
            }

            await Task.Delay(100, token);
        }

        await Task.WhenAll(tasks);
    }
}
public  class TaskFunctions
{
    private static readonly Random _random = new Random();

    public int Priority { get; set; }

    public  TaskFunctions(int priority) => Priority = priority;

    public  async Task ProcessClientRequestsAsync(CancellationToken token)
    {
        if (token.IsCancellationRequested)
            token.ThrowIfCancellationRequested();
        else
        {
            try
            {
                var startTimeTotal = DateTime.Now;
                Console.WriteLine($"Обрботка клиентского запроса: начата");
                await Task.Delay(_random.Next(3000, 10000), token);
                if (_random.Next() < 3500)
                {
                    throw new Exception("Ошибка в обрботкие клиентского запроса.");
                }
                var endTimeTotal = DateTime.Now;
                var dateFull = endTimeTotal.Subtract(startTimeTotal).TotalSeconds;                Console.WriteLine($"Обрботка клиентского запроса: завершена за {dateFull} секунд");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Обрботка клиентского запроса: отменено.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в ProcessClientRequestsAsync: {ex.Message}");
            }
        }
    }

    public  async Task DataAnalysisAsync(CancellationToken token)
    {
        if (token.IsCancellationRequested)
            token.ThrowIfCancellationRequested();
        else
        {
            try
            {
                var startTimeTotal = DateTime.Now;
                Console.WriteLine($"Анализ данных: начата");
                await Task.Delay(_random.Next(3000, 10000), token);
                if (_random.Next() < 3500)
                {
                    throw new Exception("Ошибка в анализе данных.");
                }
                var endTimeTotal = DateTime.Now;
                var dateFull = endTimeTotal.Subtract(startTimeTotal).TotalSeconds;
                
                Console.WriteLine($"Анализ данных: завершен за {dateFull} секунд");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Анализ данных: отменен.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в DataAnalysisAsync: {ex.Message}");
            }
        }
    }

    public async Task BackupAsync(CancellationToken token)
    {
        if (token.IsCancellationRequested)
            token.ThrowIfCancellationRequested();
        else
        {
            try
            {
                var startTimeTotal = DateTime.Now;
                Console.WriteLine($"Резервное копирование: начатo");
                await Task.Delay(_random.Next(3000, 10000), token);
                if (_random.Next() < 3500)
                {
                    throw new Exception("Ошибка в резервном копированиия.");
                }
                var endTimeTotal = DateTime.Now;
                var dateFull = endTimeTotal.Subtract(startTimeTotal).TotalSeconds;
               
                Console.WriteLine($"Резервное копирование: завершенo за {dateFull} секунд");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Резервное копирование: отменено.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в BackupAsync: {ex.Message}");

            }
        }
    }

    public async Task Process(CancellationToken token)
    {
        switch(Priority)
        {
            case 3:
                await ProcessClientRequestsAsync(token);
                break;
            case 2:
                await DataAnalysisAsync(token);
                break;
            case 1:
                await BackupAsync(token);
                break;
        }
    }
}

