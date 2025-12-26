/*

# ThreadHelper Class Usage Guide

The `ThreadHelper` class provides methods for safely managing thread transitions in a Windows Forms application. Here's guidance on when, how, and why to use each function:

## `RunOnThreadAsync(Func<Task> asyncFunction, Action<Exception> onError = null)`

**When to use**: 
- When you need to run a CPU-intensive or long-running operation without blocking the UI thread
- For operations that shouldn't freeze the UI (file I/O, network calls, database access)

**How to use**:
 
await ThreadHelper.RunOnThreadAsync(async () => 
{
    // Long-running code here
    await SomeTimeConsumingOperationAsync();
}, 
ex => Console.WriteLine($"Error occurred: {ex.MessageAnthropic}"));
 

**Why use it**:
- Prevents UI freezing during long operations
- Provides structured error handling
- Simplifies background processing with proper async/await patterns







## `RunOnThreadAsync<T>(Func<Task<T>> asyncFunction, Action<Exception> onError = null)`

**When to use**:
- When you need to run a background operation that returns a value
- For data processing that should happen off the UI thread

**How to use**:
 
var result = await ThreadHelper.RunOnThreadAsync(async () => 
{
    // Process that returns a value
    return await GetDataFromDatabaseAsync();
}, 
ex => LogError("Database error", ex));
 

**Why use it**:
- Returns data from background operations
- Keeps UI responsive during data processing
- Handles errors gracefully with custom error handling

## `InvokeOnUIThread(Control control, Action action)`

**When to use**:
- When updating UI elements from a background thread
- For synchronously modifying UI properties from non-UI threads

**How to use**:
 
ThreadHelper.InvokeOnUIThread(this, () => 
{
    statusLabel.Text = "Processing complete";
    progressBar.Value = 100;
});
 

**Why use it**:
- Prevents cross-thread exceptions when updating UI
- Simpler than manual InvokeRequired checks
- For quick UI updates that don't need to be awaited


 
 


## `InvokeOnUIThreadAsync(Control control, Func<Task> asyncAction)`

**When to use**:
- When you need to run async operations on the UI thread
- For complex UI updates that involve awaitable operations

**How to use**:
 
await (await ThreadHelper.InvokeOnUIThreadAsync(this, async () => 
{
    await animationManager.ShowCompletionAnimationAsync();
    await logManager.WriteToLogAsync("Operation completed");
}));
 

**Why use it**:
- Allows async operations on UI thread
- Ensures UI-dependent async operations run on correct thread
- Enables proper task completion for complex UI operations



 


## `InvokeOnUIThreadWithResultAsync<T>(Control control, Func<T> function)`

**When to use**:
- When you need a value from an operation that must run on the UI thread
- For retrieving UI state or properties from a background thread

**How to use**:
 
var selectedItems = await ThreadHelper.InvokeOnUIThreadWithResultAsync(this, () => 
{
    return listView.SelectedItems.Cast<ListViewItem>().ToList();
});
 

**Why use it**:
- Allows accessing UI-dependent values from background threads
- Returns results from UI thread operations
- Safely retrieves UI state without cross-thread exceptions

 






## `InvokeOnUIThreadWithResultAsync<T>(Control control, Func<Task<T>> asyncFunction)`

**When to use**:
- When you need a value from an async operation that must run on the UI thread
- For complex UI operations that return data and require awaiting

**How to use**:
 
var userInput = await (await ThreadHelper.InvokeOnUIThreadWithResultAsync(this, async () => 
{
    // Show dialog and wait for user input
    return await dialogManager.GetUserConfirmationAsync();
}));
 

**Why use it**:
- Allows async UI operations that return values
- Ensures complex UI interactions happen on the correct thread
- Handles task completion properly for awaitable UI operations


 

 


## `RunWithTimeoutAsync<T>(Func<Task<T>> action, TimeSpan timeout, T defaultValue = default)`

**When to use**:
- When an operation might hang or take too long
- For network calls or external services that need timeouts
- When you need to ensure operations complete within time limits

**How to use**:
 
var result = await ThreadHelper.RunWithTimeoutAsync(
    async () => await apiClient.FetchDataAsync(),
    TimeSpan.FromSeconds(10), 
    defaultValue: new EmptyResultSet());
 

**Why use it**:
- Prevents operations from hanging indefinitely
- Provides fallback values when timeouts occur
- Improves application responsiveness by limiting wait times
 


## General Guidelines

1. **Thread Safety**: Always use these helpers when crossing thread boundaries.
2. **UI Updates**: Never update UI controls directly from background threads.
3. **Error Handling**: Always handle exceptions in background operations.
4. **Task Continuations**: Use `await` with these methods rather than `.ContinueWith()`.
5. **Cancellation**: Consider adding cancellation support for long-running operations.

Following these guidelines will help maintain thread safety throughout your application and prevent UI freezing issues.

*/

namespace AnthropicToolUseBuffer.Helpers
{
    public static class ThreadHelper
    {
        /// <summary>
        /// Runs an action on a background thread with proper error handling
        /// </summary>
        public static async Task RunOnThreadAsync(Func<Task> asyncFunction, Action<Exception> onError = null)
        {
            if (asyncFunction == null) throw new ArgumentNullException(nameof(asyncFunction));

            try
            {
                await Task.Run(asyncFunction);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                // Optional: Log the exception
            }
        }

        /// <summary>
        /// Runs a function on a background thread that returns a result
        /// </summary>
        public static async Task<T> RunOnThreadAsync<T>(Func<Task<T>> asyncFunction, Action<Exception> onError = null)
        {
            if (asyncFunction == null) throw new ArgumentNullException(nameof(asyncFunction));

            try
            {
                return await Task.Run(asyncFunction);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                // Re-throw or return default based on your error handling approach
                throw;
            }
        }

        /// <summary>
        /// Executes an action on the UI thread if needed
        /// </summary>
        public static void InvokeOnUIThread(Control control, Action action)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (action == null) throw new ArgumentNullException(nameof(action));

            if (control.InvokeRequired)
            {
                control.BeginInvoke(action);
            }
            else
            {
                action();
            }
        }

 
        public static Task<Task> InvokeOnUIThreadAsync(Control control, Func<Task> asyncAction)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (asyncAction == null) throw new ArgumentNullException(nameof(asyncAction));

            var tcs = new TaskCompletionSource<Task>();

            if (control.InvokeRequired)
            {
                control.BeginInvoke(new Action(() => {
                    tcs.SetResult(asyncAction());
                }));
            }
            else
            {
                tcs.SetResult(asyncAction());
            }

            return tcs.Task;
        }

 


        /// <summary>
        /// Executes a function on the UI thread and returns a result
        /// </summary>
        public static Task<T> InvokeOnUIThreadWithResultAsync<T>(Control control, Func<T> function)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (function == null) throw new ArgumentNullException(nameof(function));

            var tcs = new TaskCompletionSource<T>();

            if (control.InvokeRequired)
            {
                control.BeginInvoke(new Action(() => {
                    try
                    {
                        T result = function();
                        tcs.SetResult(result);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }));
            }
            else
            {
                try
                {
                    T result = function();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }

            return tcs.Task;
        }

        /// <summary>
        /// Executes an async function on the UI thread that returns a result
        /// </summary>
        public static Task<Task<T>> InvokeOnUIThreadWithResultAsync<T>(Control control, Func<Task<T>> asyncFunction)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (asyncFunction == null) throw new ArgumentNullException(nameof(asyncFunction));

            var tcs = new TaskCompletionSource<Task<T>>();

            if (control.InvokeRequired)
            {
                control.BeginInvoke(new Action(() => {
                    tcs.SetResult(asyncFunction());
                }));
            }
            else
            {
                tcs.SetResult(asyncFunction());
            }

            return tcs.Task;
        }

        /// <summary>
        /// Runs a delegate on the UI thread with a timeout
        /// </summary>
        public static async Task<T> RunWithTimeoutAsync<T>(Func<Task<T>> action, TimeSpan timeout, T defaultValue = default)
        {
            var task = action();
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout));

            if (completedTask == task)
            {
                return await task; // Task completed within timeout
            }

            // Timeout occurred
            return defaultValue;
        }
    }

}
