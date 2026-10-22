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

## Phase 2: CPU-bound work and asynchronous I/O

Task.Run fits because its cpu bound so the threed pool thread is busy executing calculation ,
    but still no threed blocek , the threead is free to do another work as the calculation is moved to another thread.

calling database is a I/O bound, there is no need to still waiting 
the method suspended at await and the thread that was executing the method can return to the thraed pool to do another work

## Phase 2 questions

1) starting a task : telling the work to begin
   await task : wait until the task finished.

2) it's not neccessary to create new thread for every task , as the thread pool can reuse the thread to do another work,
 also some taks like I/O don't need thread while waiting.

 Task.Delay doesn't use the thread while waiting , when await it the method suspended , allowing the thread to do another work 

3) Result or Wait() block the current thread unitl the task completes

      
         public static void BlockingDemo()
        {
            Task task = Task.Delay(2000);

            task.Wait(); // Blocks the current thread for up to 2 seconds.

             Console.WriteLine("Task completed");
        }

4) when an exception thrown inseide task that never awaited , we will miss this exception as the exception is stored inside the task,
    we can cath it later by awaiting the task using try/catch

5) only use Task.Run for cpu bound tasks and avoid it for I/O bound tasks.

6) Task returns nothing , Task<T> return the value of type T when the task completes .

7) lock block cannt contain await because it's designed for synchronous operations and the lock must be release by the thread the acquired it.

8) the unprotected stock reservation caused overselling because there was individual lock over the file , lock for read and lock for write.

    solved by creating a lock through the while read,update and write operation using UpdateAsync method .


## Phase 2 questions

1) not neccessary , it just allow threads to do other work during the operation is waiting for response.

2) because the thread don't need to be blocked during waiting for response , so the application can handle many requests concurrently.

3) the application will handle the requests sequentially , so it takes huge time to process.

4) because there is no need to occupy the threads with just waiting calling database respone , async/await for I/O bound
 allow threads to do other work until task finishes.

