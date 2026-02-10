# Coding Style Guide

This document describes the C#-specific coding conventions for this project.

---

## Core Principles

These are the core principles that define how we write code.

### 1. TYPE INFERENCE - CONTROLLED USE OF `var`

Type inference with `var` is **tightly controlled** but not forbidden.

**Core Principle:** Code must be understandable without IDE assistance. The reader is more important than the writer.

**Allowed - Type is obvious from right-hand side:**
```csharp
// ALLOWED - Constructor visible.
var buffer = new byte[1024];
var stream = new FileStream(path, FileMode.Open);
var players = new List<Player>();

// ALLOWED - Factory method makes type clear.
var builder = new StringBuilder();
var client = new HttpClient();
```

**Forbidden - Meaning is hidden:**
```csharp
// WRONG - Return type unclear.
var result = CalculateValue(x, y, z);
var data = GetData();

// CORRECT - Explicit types are self-documenting.
int result = CalculateValue(x, y, z);
List<byte> data = GetData();
```

**Rule:** If understanding the type requires jumping to a definition, `var` is forbidden.

**Why?** Code must be understandable without IDE assistance. The reader is more important than the writer.

### 2. SINGLE-LINE CODE - NO WRAPPING

**Code must fit in the reader's working memory. If it does not fit on one line, it does not fit in the head either.**

Control flow, conditions, and signatures must stay on single lines. This enforces **locality of understanding**: all required information must be visible in one visual frame.

```csharp
// GOOD - all parameters visible, even if line is long.
public void ProcessData(string source, string target, bool validate, int quality, ProcessingMode mode)
{
	// You can see everything. No hidden coupling. No indirection.
}

// WRONG - wrapping hides complexity.
public void ProcessData(
	string source,
	string target,
	bool validate
)
{
	// Now you have to scan vertically. Context is distributed.
}

// ALSO WRONG - abstraction hides parameters.
public class ProcessingConfig { /* ... */ }
public void ProcessData(ProcessingConfig config)
{
	// Now you can't see what the method needs.
	// Coupling is hidden. Debugging is harder.
}

// CORRECT - condition visible.
if (condition1 && condition2 && condition3)
{
	DoSomething();
}
```

**Why single-line?**
- **Visibility over abstraction** - You can see all parameters/conditions directly. No indirection. No hidden coupling.
- Your brain has ~7±2 working memory slots. Single-line keeps everything in one frame.
- Wrapping distributes complexity vertically - makes you scan and reconstruct context
- Creating parameter objects to "fix" long lines makes things WORSE: hidden coupling, loss of transparency, harder debugging
- A long single line is honest. It shows the real complexity. That's good.

**Don't wrap. Don't hide. If it's long, it's long. That's the truth.**

**Exception - LINQ chains may wrap when linear:**
```csharp
// Linear chains may wrap.
List<string> validFiles = files
	.Where(f => File.Exists(f))
	.ToList();
```

### 3. ALLMAN BRACES - ALWAYS ON NEW LINE
Opening braces `{` ALWAYS go on a new line. No exceptions (except single-line lambdas).

```csharp
// WRONG.
if (condition) {
	DoSomething();
}

// CORRECT.
if (condition)
{
	DoSomething();
}
```

**Why?** Visual symmetry makes code easier to scan and spot errors.

### 4. TABS FOR INDENTATION - NOT SPACES
Use tabs, period. Configure your editor properly.

Why? Because a single tab character is the true, unambiguous representation of a single indentation level. Spaces are a visual approximation; tabs are the logical unit.

### 5. COMPLETE SENTENCES IN COMMENTS
Comments start with capital letter, end with period.

```csharp
// WRONG.
// calculate average value

// CORRECT.
// Calculate the average value.
```

**Why?** Professional code looks professional. We're not writing text messages.

---

## Naming Conventions

Get the names right or the code gets rejected.

### Private Fields
Use **_camelCase starting with underscore**:

```csharp
private readonly string _filePath;
private readonly int _bufferSize;
private int _processedCount;
```

### Public Members, Methods, and Properties
Use **PascalCase** with no underscores:

```csharp
public string ConnectionString { get; set; }
public int MaxRetries { get; set; }

public void ProcessData(string input)
{
	// Implementation.
}

public bool ValidateInput(string data)
{
	// Implementation.
}
```

### Local Variables and Parameters
Use **camelCase** with no underscores:

```csharp
public void CalculateStatistics(byte[] data, int width, int height)
{
	double averageValue = 0.0;
	int totalPixels = width * height;
	
	for (int i = 0; i < data.Length; i++)
	{
		averageValue += data[i];
	}
}
```

### Constants
Use **SCREAMING_SNAKE_CASE**:

```csharp
public const string APP_NAME = "Application";
public const string APP_VERSION = "1.0.0";
public const int MAX_BUFFER_SIZE = 10_000_000;
private const int DEFAULT_TIMEOUT = 30;
```

### Interfaces
Prefix with `I` and use **PascalCase**:

```csharp
public interface IFileProcessor
{
	void Process(string filePath);
	bool Validate(string data);
}
```

---

## Formatting Rules

### Indentation
- **Tabs only** - no spaces for indentation.
- One tab per level.
- Continuation lines get one additional tab.

### Braces Placement
Opening brace `{` on new line for:
- Classes, structs, interfaces, enums
- Namespaces
- Methods and properties
- If/else blocks
- Loops (for, foreach, while, do-while)
- Switch statements
- Try/catch/finally blocks
- Multi-line lambdas

**Examples:**

```csharp
public class FileProcessor
{
	private readonly string _filePath;

	public void Process()
	{
		// Implementation.
	}
}

public struct ProcessingResult
{
	public long OriginalSize { get; set; }
	public long NewSize { get; set; }
}

public enum ProcessingMode
{
	Fast,
	Balanced,
	Quality
}

if (condition)
{
	// If body.
}

foreach (string file in files)
{
	// Loop body.
}
```

**Exception - Single-line lambdas:**

```csharp
files.Sort((a, b) => a.Length.CompareTo(b.Length));
```

### Method Signatures
**All parameters on a single line:**

```csharp
public void ProcessData(string source, string target, bool validate, int quality, ProcessingMode mode)
{
	// Implementation.
}

public ProcessingResult ProcessComplexOperation(string sourcePath, string targetPath, ProcessingOptions options, Action<string> callback)
{
	// Implementation.
}
```

If it's too long, you're doing too much - refactor it.

### Spacing Rules

**Between methods - one blank line:**
```csharp
public void MethodOne()
{
	// Implementation.
}

public void MethodTwo()
{
	// Implementation.
}
```

**Within methods - blank lines separate logic:**
```csharp
public void ProcessData()
{
	// Section 1: Read data.
	byte[] data = File.ReadAllBytes(path);
	int size = data.Length;
	
	// Section 2: Process data.
	byte[] processed = Transform(data);
	byte[] optimized = Optimize(processed);
	
	// Section 3: Write results.
	File.WriteAllBytes(outputPath, optimized);
}
```

**Between independent control structures - blank lines:**
```csharp
// CORRECT - blank lines separate independent checks.
if (p != -1)
{
	string key = attr.Key.Substring(p + 1);
}

if (_params.DumpIdAttributesName)
{
	outAttr.Append($"{key}={attr.Value}");
}
```

**Related if/else stays together - no blank lines:**
```csharp
if (condition1)
{
	DoSomething();
}
else if (condition2)
{
	DoSomethingElse();
}
else
{
	DoDefault();
}
```

**Critical spacing rules:**
- **Never more than one blank line** anywhere.
- **No trailing spaces** at end of lines.
- **No excessive spacing** like `if (x == 0)   `.

---

## Control Flow

### When to Use If vs Switch

**Use if/else for 1-2 comparisons:**
```csharp
if (priority == 1)
{
	ProcessHighPriority();
}
else
{
	ProcessNormalPriority();
}
```

**Use switch for 3+ comparisons:**
```csharp
switch (priority)
{
	case 1:
	case 2:
	case 3:
		ProcessHighPriority();
		break;
	
	case 4:
	case 5:
		ProcessMediumPriority();
		break;
	
	default:
		ProcessLowPriority();
		break;
}
```

**Switch expressions (C# 8+) for 3+ cases:**
```csharp
string priorityDesc = priority switch
{
	1 or 2 or 3 => "High priority",
	4 or 5 => "Medium priority",
	_ => "Low priority"
};
```

### If-Else Statements
- **Always use braces** - even for single statements.
- **Keep conditions on single line** - no wrapping.

```csharp
// CORRECT.
if (cursor[1] == '!' && cursor[2] == '[' && cursor[3] == 'C' && cursor[4] == 'D')
{
	HandleCDATA();
}

// WRONG - no braces.
if (condition)
	DoSomething();
```

### Switch Statements
- **Always include default case**.
- **Always include break** (unless fall-through is intentional and commented).

```csharp
switch (mode)
{
	case ProcessingMode.Fast:
		ApplyFastProcessing();
		break;
	
	case ProcessingMode.Balanced:
		InitializeBalanced();
		ApplyBalancedProcessing();
		LogResult();
		break;
	
	default:
		Console.WriteLine("Unknown processing mode");
		break;
}
```

### Loops
Use the appropriate loop type:

```csharp
// For loops - known iteration count.
for (int i = 0; i < count; i++)
{
	ProcessItem(i);
}

// Foreach - iterating collections.
foreach (string file in files)
{
	ProcessFile(file);
}

// While loops - condition-based iteration.
while (queue.Count > 0)
{
	string item = queue.Dequeue();
	ProcessItem(item);
}
```

---

## Type Declarations

### The Rule: Controlled Use of `var`

Type inference with `var` is allowed when the type is obvious, forbidden when meaning is hidden.

**Use `var` when the type is obvious:**
```csharp
// Good - constructor clearly visible.
var buffer = new byte[1024];
var stream = new FileStream(path, FileMode.Open);
var players = new List<Player>();

// Good - factory methods make type clear.
var builder = new StringBuilder();
var client = new HttpClient();
```

**Do NOT use `var` when type is unclear:**
```csharp
// WRONG - type inference hides information.
var result = CalculateValue(x, y, z);
var file = OpenFile(path);
var data = GetData();

// CORRECT - explicit types are self-documenting.
int result = CalculateValue(x, y, z);
FileHandle file = OpenFile(path);
List<byte> data = GetData();
```

### Property Declarations
Always explicit:

```csharp
public class Configuration
{
	public string FilePath { get; set; }
	public int BufferSize { get; set; }
	public ProcessingMode Mode { get; set; }
	public List<string> AllowedExtensions { get; set; }
}
```

### Nullable Reference Types
Use explicit nullable annotations:

```csharp
public class FileInfo
{
	public string FilePath { get; set; } // Non-nullable.
	public string? Description { get; set; } // Nullable.
	public DateTime? LastModified { get; set; } // Nullable value type.
}
```

---

## Comments

### General Rules
- **Start with capital letter**.
- **End with period**.
- **Use complete sentences**.
- **Use `//` for single-line** comments.
- **Use `/* */` for multi-line** comments.

```csharp
// Calculate the size reduction percentage.
double reductionPct = (1.0 - ((double)newSize / originalSize)) * 100.0;

/*
 * This is a multi-line comment explaining
 * a complex algorithm or process flow.
 */
```

### Documentation Comments
Use XML documentation `///` for public members:

```csharp
/// <summary>
/// Processes a file using the specified options.
/// If validation is enabled, performs additional checks before processing.
/// </summary>
/// <param name="sourcePath">Path to the source file.</param>
/// <param name="targetPath">Path where output will be saved.</param>
/// <param name="validate">Whether to perform validation checks.</param>
/// <returns>ProcessingResult with statistics.</returns>
public ProcessingResult ProcessFile(string sourcePath, string targetPath, bool validate)
{
	// Implementation.
}
```

### Inline Comments
Only when clarifying non-obvious code:

```csharp
Color newPixel = Color.FromArgb(
	QuantizeChannel(oldPixel.R, factor),
	QuantizeChannel(oldPixel.G, factor),
	QuantizeChannel(oldPixel.B, factor),
	oldPixel.A // Keep alpha unchanged.
);
```

**Don't state the obvious:**
```csharp
// WRONG - obvious comment.
int x = 5; // Set x to 5.

// CORRECT - only comment when adding value.
int retryCount = 5; // Empirically determined optimal retry count.
```

---

## Using Statements

### Using Directives
Place at the top of the file:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MyApplication
{
	// Code.
}
```

### Using for IDisposable
Always use `using` for IDisposable:

```csharp
// Modern using declaration (C# 8+).
using FileStream stream = File.OpenRead(path);
byte[] data = new byte[stream.Length];
stream.Read(data, 0, data.Length);

// Traditional using statement.
using (StreamReader reader = new StreamReader(path))
{
	string line = reader.ReadLine();
	ProcessLine(line);
}
```

---

## Classes and Object-Oriented Design

### Class Structure
Organize class members in the following order:

1. Private fields
2. Constructors
3. Properties
4. Public methods
5. Private methods

```csharp
public class FileProcessor
{
	// Private fields.
	private readonly string _filePath;
	private readonly int _bufferSize;
	private int _processedCount;
	
	// Constructors.
	public FileProcessor(string filePath, int bufferSize)
	{
		_filePath = filePath;
		_bufferSize = bufferSize;
		_processedCount = 0;
	}
	
	// Properties.
	public string FilePath => _filePath;
	public int ProcessedCount => _processedCount;
	
	// Public methods.
	public ProcessingResult Process()
	{
		// Implementation.
	}
	
	public void Reset()
	{
		_processedCount = 0;
	}
	
	// Private methods.
	private void ValidateInput()
	{
		// Implementation.
	}
}
```

### Properties
Use auto-properties when possible:

```csharp
public class Configuration
{
	public string FilePath { get; set; }
	public int BufferSize { get; set; }
	
	// Read-only property.
	public string Version { get; } = "1.0.0";
	
	// Property with validation.
	private int _maxRetries;
	public int MaxRetries
	{
		get => _maxRetries;
		set
		{
			if (value < 0)
			{
				throw new ArgumentException("MaxRetries must be non-negative");
			}
			_maxRetries = value;
		}
	}
}
```

### Interfaces
```csharp
public interface IFileProcessor
{
	ProcessingResult Process(string filePath);
	bool Validate(string data);
	Task<ProcessingResult> ProcessAsync(string filePath);
}

public class FileProcessor : IFileProcessor
{
	public ProcessingResult Process(string filePath)
	{
		// Implementation.
	}
	
	public bool Validate(string data)
	{
		// Implementation.
	}
	
	public async Task<ProcessingResult> ProcessAsync(string filePath)
	{
		// Implementation.
	}
}
```

---

## Error Handling

### Use Exceptions (With Boundaries)

Prefer exceptions for error handling in cold code.

**Exceptions are allowed in:**
- Application startup and configuration
- I/O operations (file, network, database)
- User input validation
- Initialization and setup code

**Exceptions are forbidden in:**
- Real-time loops (game ticks, frame updates)
- Network packet handlers
- Per-frame rendering
- Performance-critical inner loops
- Hot paths with tight latency requirements

**Why?** Exceptions in C# allocate, unwind the stack, and destroy branch prediction. Fine for exceptional cases; forbidden in hot paths.

```csharp
public ProcessingResult ProcessFile(string path)
{
	if (!File.Exists(path))
	{
		throw new FileNotFoundException($"File not found: {path}");
	}
	
	FileInfo info = new FileInfo(path);
	
	if (info.Length > MAX_FILE_SIZE)
	{
		throw new InvalidOperationException($"File too large: {info.Length} bytes");
	}
	
	// Main processing logic.
	return new ProcessingResult();
}

// In hot paths, use return codes or Result<T> patterns instead.
public bool TryProcessPacket(byte[] data, out GamePacket packet)
{
	packet = null;
	
	if (data.Length < MIN_PACKET_SIZE)
	{
		return false; // No exception in hot path.
	}
	
	packet = ParsePacket(data);
	return true;
}
```

### Try-Catch-Finally
```csharp
public void ProcessFiles(List<string> files)
{
	foreach (string file in files)
	{
		try
		{
			ProcessingResult result = ProcessFile(file);
			Console.WriteLine($"Processed: {file}");
		}
		catch (FileNotFoundException ex)
		{
			Console.WriteLine($"Error: {ex.Message}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Unexpected error: {ex.Message}");
			throw;
		}
	}
}
```

### Custom Exceptions
```csharp
public class ProcessingException : Exception
{
	public ProcessingException(string message) : base(message)
	{
	}
	
	public ProcessingException(string message, Exception innerException) : base(message, innerException)
	{
	}
}
```

---

## String Handling

### String Interpolation
Use string interpolation instead of concatenation:

```csharp
// CORRECT.
string message = $"Processed {count} files in {elapsed} seconds";

// WRONG - concatenation.
string message = "Processed " + count + " files in " + elapsed + " seconds";
```

### StringBuilder for Multiple Concatenations
```csharp
// Good - StringBuilder for loops.
StringBuilder sb = new StringBuilder();
for (int i = 0; i < 1000; i++)
{
	sb.Append($"Line {i}\n");
}
string result = sb.ToString();

// Avoid - string concatenation in loops.
string result = "";
for (int i = 0; i < 1000; i++)
{
	result += $"Line {i}\n"; // Creates new string each iteration.
}
```

---

## Numeric Formatting

### Digit Separators (C# 7.0)
Use underscores for digit separators:

```csharp
int largeNumber = 1_000_000;
long veryLarge = 1_234_567_890;
double precise = 3.141_592_653;

const int MAX_BUFFER_SIZE = 10_000_000;
```

### Hexadecimal
```csharp
int hexValue = 0xFF_FF_FF;
```

---

## Collections and LINQ

### Common Collections
```csharp
// List - dynamic array.
List<string> files = new List<string>();
files.Add("file1.txt");
files.Add("file2.txt");

// Dictionary - key-value pairs.
Dictionary<string, int> ages = new Dictionary<string, int>();
ages["Alice"] = 30;
ages["Bob"] = 25;

// HashSet - unique elements.
HashSet<int> uniqueNumbers = new HashSet<int>();
uniqueNumbers.Add(42);
uniqueNumbers.Add(17);
uniqueNumbers.Add(42); // Ignored - already exists.

// Queue - FIFO.
Queue<string> queue = new Queue<string>();
queue.Enqueue("first");
queue.Enqueue("second");
string item = queue.Dequeue();

// Stack - LIFO.
Stack<string> stack = new Stack<string>();
stack.Push("first");
stack.Push("second");
string top = stack.Pop();
```

### LINQ - Use Method Syntax

Use method syntax (not query syntax).

**LINQ is allowed for:**
- Filtering and projection
- Data queries and aggregation
- Reporting and I/O boundaries
- Configuration processing

**LINQ is forbidden in:**
- Game loops and real-time systems
- Network packet handlers
- Parsers and tight inner loops
- Performance-critical hot paths

```csharp
// GOOD - LINQ for queries and projections.
List<string> validFiles = files
	.Where(f => File.Exists(f))
	.Where(f => new FileInfo(f).Extension == ".txt")
	.ToList();

int totalSize = files
	.Select(f => new FileInfo(f).Length)
	.Sum();

// WRONG - query syntax.
List<string> validFiles = (from f in files
						   where File.Exists(f)
						   select f).ToList();

// AVOID - LINQ in hot path.
// Use explicit loop instead for performance-critical code.
for (int i = 0; i < players.Count; i++)
{
	if (players[i].Health > 0)
	{
		activePlayers.Add(players[i]);
	}
}
```

**Why the boundaries?** LINQ hides allocation, boxing, and closure captures. Fine for queries; forbidden in hot paths.

### Common LINQ Operations
```csharp
List<int> numbers = new List<int> { 5, 2, 8, 1, 9 };

// Where - filter.
List<int> evens = numbers.Where(n => n % 2 == 0).ToList();

// Select - transform.
List<int> doubled = numbers.Select(n => n * 2).ToList();

// OrderBy - sort.
List<int> sorted = numbers.OrderBy(n => n).ToList();

// First/FirstOrDefault - get first element.
int first = numbers.First(n => n > 5);
int firstOrDefault = numbers.FirstOrDefault(n => n > 100); // Returns 0 if not found.

// Any/All - check conditions.
bool hasNegative = numbers.Any(n => n < 0);
bool allPositive = numbers.All(n => n > 0);

// Count.
int count = numbers.Count(n => n > 5);

// Sum/Average/Min/Max.
int sum = numbers.Sum();
double average = numbers.Average();
int min = numbers.Min();
int max = numbers.Max();
```

---

## Modern C# Features

### Records (C# 9+)
Use for immutable data:

```csharp
public record ProcessingResult(long OriginalSize, long NewSize, double ReductionPercent);

// Usage.
ProcessingResult result = new ProcessingResult(1000, 800, 20.0);
```

### Pattern Matching
```csharp
public string ProcessValue(object value)
{
	return value switch
	{
		int i => $"Integer: {i}",
		string s => $"String: {s}",
		ProcessingResult r => $"Result: {r.ReductionPercent}%",
		null => "Null value",
		_ => "Unknown type"
	};
}
```

### Null-Coalescing Operators
```csharp
string fileName = inputName ?? "default.txt";

_cachedData ??= LoadData(); // Assign only if null.
```

### Async/Await

**Async is not free.** It introduces allocation, context switches, and latency overhead.

**Use async for:**
- I/O operations (file, network, database)
- User interface responsiveness
- Long-running background tasks

**Avoid async in:**
- Hot paths and tight loops
- Game tick / frame updates
- Real-time packet handlers
- Performance-critical inner loops

Suffix async methods with `Async`:

```csharp
public async Task<ProcessingResult> ProcessFileAsync(string path)
{
	byte[] data = await File.ReadAllBytesAsync(path);
	ProcessingResult result = await ProcessDataAsync(data);
	return result;
}
```

Use `ConfigureAwait(false)` in library code:

```csharp
await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
```

**Never use async void** (except event handlers):

```csharp
// WRONG.
public async void ProcessData() // Can't be awaited.
{
}

// CORRECT.
public async Task ProcessDataAsync() // Can be awaited.
{
}

// Exception - event handlers.
private async void Button_Click(object sender, EventArgs e)
{
	await ProcessDataAsync();
}
```

**Performance note:** Async state machines allocate. Prove async is necessary before using it in performance-sensitive code.

---

## Performance Considerations

### Garbage Collection Is Not Free

**Garbage collection is not free.** Avoid allocations in hot paths. Prefer object pooling or stack allocation where possible.

```csharp
// AVOID - allocation in hot path.
for (int i = 0; i < 10000; i++)
{
	var temp = new GameObject(); // GC pressure.
	ProcessFrame(temp);
}

// PREFER - object pooling.
private Queue<GameObject> _objectPool = new Queue<GameObject>();

for (int i = 0; i < 10000; i++)
{
	GameObject obj = _objectPool.Count > 0 ? _objectPool.Dequeue() : new GameObject();
	ProcessFrame(obj);
	_objectPool.Enqueue(obj); // Reuse.
}

// PREFER - stack allocation with Span<T>.
Span<int> buffer = stackalloc int[100];
ProcessData(buffer); // No heap allocation.
```

### Use StringBuilder
```csharp
// Good - StringBuilder for multiple concatenations.
StringBuilder sb = new StringBuilder();
for (int i = 0; i < 1000; i++)
{
	sb.Append("Item ");
	sb.Append(i);
}
string result = sb.ToString();
```

### Avoid Boxing
```csharp
// Bad - boxing value type.
int number = 42;
object boxed = number; // Boxing.

// Good - use generics to avoid boxing.
List<int> numbers = new List<int>(); // No boxing.
```

### Use Span<T> for Performance
```csharp
// Good - Span for stack allocation.
Span<int> numbers = stackalloc int[100];
for (int i = 0; i < numbers.Length; i++)
{
	numbers[i] = i;
}
```

---

## Anti-Patterns to Avoid

### ❌ Don't Use `var` When Type Is Unclear
```csharp
// WRONG - unclear return type.
var result = CalculateValue(x, y, z);

// CORRECT - explicit type.
int result = CalculateValue(x, y, z);

// ALLOWED - obvious constructor.
var buffer = new byte[1024];
```

### ❌ Don't Declare Multiple Variables on Same Line
```csharp
// WRONG - confusing.
int a, b = 0;
int x = 1, y = 2, z;

// CORRECT - one per line.
int a = 0;
int b = 0;
int x = 1;
int y = 2;
int z = 0;
```

### ❌ Don't Over-Engineer - Avoid Single-Use Code
```csharp
// WRONG - constant used only once.
private const int BUFFER_SIZE = 1024;
byte[] buffer = new byte[BUFFER_SIZE];

// CORRECT - inline it.
byte[] buffer = new byte[1024];

// WRONG - helper method called only once.
private void PrintSeparator()
{
	Console.WriteLine("---");
}

// CORRECT - inline it.
Console.WriteLine("---");
```

**Exception:** Create abstractions when used multiple times, improves clarity significantly, or likely to change.

### ❌ Don't Ignore IDisposable
```csharp
// WRONG - easy to forget to dispose.
FileStream stream = File.OpenRead(path);
// ...
stream.Dispose();

// CORRECT - using statement.
using FileStream stream = File.OpenRead(path);
// Automatically disposed.
```

### ❌ Don't Use Magic Numbers
```csharp
// WRONG.
if (status == 200) // What does 200 mean?
{
	// Process.
}

// CORRECT.
private const int HTTP_STATUS_OK = 200;

if (status == HTTP_STATUS_OK)
{
	// Process.
}
```

### ❌ Don't Write Deeply Nested Code
```csharp
// WRONG - deeply nested.
if (condition1)
{
	if (condition2)
	{
		if (condition3)
		{
			// Too deep.
		}
	}
}

// CORRECT - early returns.
if (!condition1)
{
	return;
}

if (!condition2)
{
	return;
}

if (!condition3)
{
	return;
}

// Main logic at top level.
```

### ❌ Don't Worship Frameworks - Justify Abstractions

Frameworks and libraries must demonstrably reduce complexity.

**Avoid without justification:**
- Dependency Injection containers (Autofac, Unity, Castle Windsor)
- ORMs with excessive magic (Entity Framework's lazy loading, automatic migrations)
- Reflection-heavy designs
- Runtime code generation
- Attribute-based "magic" behavior

**Why?** These patterns sacrifice:
- Debuggability (stack traces become unintelligible)
- Predictability (control flow hidden behind framework calls)
- Performance (reflection, dynamic dispatch, allocations)
- Simplicity (indirection and abstraction overhead)

```csharp
// AVOID - framework magic hiding control flow.
[Inject]
public IUserService UserService { get; set; } // Where does this come from?

// PREFER - explicit construction and dependencies.
private readonly IUserService _userService;

public UserController(IUserService userService)
{
	_userService = userService;
}

// AVOID - ORM magic with hidden queries.
var users = context.Users.Include(u => u.Orders).ToList(); // N+1 queries? Lazy loading?

// PREFER - explicit queries with known cost.
var users = context.Users
	.Where(u => u.Active)
	.Select(u => new UserDto { Id = u.Id, Name = u.Name })
	.ToList();
```

**Rule:** If you can't explain what a framework does without saying "it just works", don't use it.

---

## Quick Reference Checklist

Before submitting code, verify:

- [ ] **Controlled `var` use** - only when type is obvious from RHS
- [ ] **Explicit types when unclear** - I can see what every variable is without jumping to definitions
- [ ] **LINQ boundaries** - for queries only, not hot paths or core logic
- [ ] **Async cost awareness** - avoid in performance-critical loops
- [ ] **Exception boundaries** - no exceptions in real-time systems or inner loops
- [ ] **Framework skepticism** - no DI/ORM/reflection unless justified
- [ ] **GC awareness** - avoid allocations in hot paths, prefer pooling or stack allocation
- [ ] **Single-line code** - all control flow, conditions, signatures on single lines (no wrapping)
- [ ] **Allman braces** - opening `{` on new line
- [ ] **Tabs for indentation** - not spaces
- [ ] **Complete sentences in comments** - capital letter, period
- [ ] **_camelCase** for private fields (with underscore)
- [ ] **PascalCase** for public members/methods/properties/classes
- [ ] **camelCase** for local variables/parameters
- [ ] **SCREAMING_SNAKE_CASE** for constants
- [ ] **One blank line** between methods
- [ ] **Never more than one blank line** anywhere
- [ ] **No trailing spaces**
- [ ] **One variable per line** - no `int a, b;`
- [ ] **Switch for 3+ cases** - if/else for 1-2
- [ ] **No single-use constants/helpers**
- [ ] **Always use `using`** for IDisposable
- [ ] **Suffix async methods** with `Async`
- [ ] **Never async void** (except event handlers)
- [ ] **String interpolation** instead of concatenation
- [ ] **LINQ method syntax** not query syntax

---

## Summary

Remember these core principles:

1. **Controlled Type Inference** - Use `var` only when type is obvious from RHS.
2. **LINQ Boundaries** - For queries and projections, not hot paths or core logic.
3. **Async Cost Awareness** - Async is not free; avoid in performance-critical loops.
4. **Exception Boundaries** - No exceptions in real-time systems or inner loops.
5. **GC Awareness** - Avoid allocations in hot paths; prefer pooling or stack allocation.
6. **Framework Skepticism** - No DI containers, ORMs, or reflection unless justified.
7. **Single-Line Code** - All control flow, conditions, signatures on single lines. Wrapping hides complexity instead of reducing it.
8. **Allman Braces** - Opening braces on new lines always.
9. **Complete Sentences** - Comments are documentation.
10. **Don't Over-Engineer** - YAGNI (You Ain't Gonna Need It).
11. **Dispose Properly** - Always use `using` for IDisposable.
