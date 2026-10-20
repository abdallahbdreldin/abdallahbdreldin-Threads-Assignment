## Json File Store Design

# Overview

    Json File Store is responsible for reading and writing JSON files that are used to save the applications collections. It brings together file access, JSON serialization and JSON deserialization, error handling and synchronization.
    
    Each collection is saved as a JSON file inside the Data folder. The store uses System Text Json for serialization.
    
  # API Design
    
    The class offers methods for regular application tasks and synchronous methods, for comparing synchronous and asynchronous processing.
    
# Methods

   1)  Read Async <T>() :

    Converts a JSON file in an asynchronous way. Provides an instance of T if the file is not found.

   2) Write Async <T>() :

    Converts data and saves it to a JSON file in a way.

   3) Update Async <T>() :

    Carries out a read modify write action while keeping a lock on the file.

   4) Read <T>() :

    Reads and converts a JSON file in a way.

   5)  Write <T>() :

    Converts. Saves data in a synchronous way.

## Phase 1 comparison questions

1. **Why are the synchronous and asynchronous times almost the same when orders are awaited one after another?**  

   Both versions perform the same operations in sequence, and each operation waits for the same simulated latency.
   `await` lets the current thread do other work during an asynchronous wait,
   but it does not make this sequential run finish sooner by itself.

   note that : Async operations perform one more operation than the Sync operations (SaveOrder) so the async operation took more time .

2. **Is the thread ID before an `await` always the same as after it? Why?**  

   No. When an awaited operation is incomplete, the continuation can run on a different thread.
   A console application normally has no synchronization context that requires the continuation to return to its original thread.
   
   ![thread while Sync](screenshots/SampleSyncOperations.png)
   ![threads while ASync](screenshots/SampleAsyncOperations.png)

3. **What does the thread do while `Task.Delay()` or `Thread.Sleep()` is waiting?**  

   During `Task.Delay()`, the method is suspended and the thread can run other work.
   During `Thread.Sleep()`, the current thread is blocked and cannot do other work until the sleep ends.

4. **Why should a repository method return `Task<T>` instead of `T`?**  

   A repository often waits for file, database, or network I/O. Returning `Task<T>` lets the caller await that operation without blocking its thread,
   and provides the eventual result as `T`.

5. **What is the difference between `File.ReadAllText` and `File.ReadAllTextAsync`? Does the async version occupy a thread while the disk is reading?**  

   `File.ReadAllText` blocks its caller until the whole file has been read.
   `File.ReadAllTextAsync` returns a task that can be awaited, so the caller's thread is not blocked while the I/O is pending.

![Summary](screenshots/summary.png)
