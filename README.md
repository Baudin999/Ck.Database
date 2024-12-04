# JsonDatabase for .NET

## Overview

**JsonDatabase** is a lightweight, human-readable database solution for .NET projects. It uses JSON for data storage, providing a simple way to store, retrieve, and manipulate objects in a file-based database. The database is highly performant and supports partial updates, making it ideal for applications that require a fast and easy-to-use storage layer.

---

## Features

- **Human-readable storage**: Data is stored in JSON format, making it easy to read and debug.
- **Simple API**: Store, retrieve, and delete objects with minimal setup.
- **Partial updates**: Supports efficient updates to specific parts of the database file.
- **Typed operations**: Operate directly on your classes without additional mapping layers.
- **File-based**: Uses the local file system, with no external dependencies or server setup.

---

## Installation

To install **Ck.Database**, add the NuGet package to your project. 

### Using Package Manager Console
```bash
Install-Package Ck.Database -Version 1.0.0
```

### Using .NET CLI
```bash
dotnet add package Ck.Database --version 1.0.0
```

### Adding to .csproj
Add the following line to your `.csproj` file:
```xml
<PackageReference Include="Ck.Database" Version="1.0.0" />
```

For more information, visit the [Ck.Database NuGet page](https://www.nuget.org/packages/Ck.Database/1.0.0).

---

## Usage

### 1. Initializing the Database

Set up the database by specifying a directory for data storage:

```csharp
var Path = @"C:\TEMP\TestDb";
var JsonDatabase = new JsonDatabase(Path);
```

### 2. Storing Data

Create objects to store in the database:

```csharp
var foo = new Foo
{
    Name = "Carlos's World",
    Bar = new Bar
    {
        Street = "Weezenhof 6134",
        Drink = new Drink { Name = "White wine" }
    },
    Somethings = new List<Drink>
    {
        new Drink { Name = "Gin & Tonic" },
        new Drink { Name = "Beer" },
        new Drink { Name = "Whiskey" },
    }
};
JsonDatabase.Store(foo);
```

### 3. Deleting Data

Remove specific objects from the database by their type and `Id`:

```csharp
var whiskeyId = foo.Somethings[2].Id;
JsonDatabase.Delete<Drink>(whiskeyId);
foo.Somethings.Remove(2);
```

---

## Example Classes

Here’s an example of the classes used in the database:

```csharp
public class Foo
{
    public string Name { get; set; }
    public Bar Bar { get; set; }
    public List<Drink> Somethings { get; set; }
}

public class Bar
{
    public string Street { get; set; }
    public Drink Drink { get; set; }
}

public class Drink
{
    public string Name { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid(); // Ensure each object has a unique identifier
}
```

---

## Design Philosophy

JsonDatabase focuses on simplicity and ease of use:
- No complex schema definitions.
- Clear and concise API for rapid development.
- Transparent file-based storage.

---

## Contribution

Contributions to **JsonDatabase** are welcome! Please submit issues or pull requests through the GitHub repository.

---

## License

This project is licensed under the MIT License. See the LICENSE file for details.

---

Happy coding! 😊