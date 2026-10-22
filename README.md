# Staybnb

Staybnb is a web application developed for the Programming 742 module. The project uses ASP.NET Core MVC and separates its application logic, data models, and user-interface pages.

## Project Overview

The project contains pages and code for:

- Property-related information
- An inbox and message pages
- Notifications
- User-related data
- Database access and migrations

## Technologies

- C#
- .NET 8
- ASP.NET Core MVC
- Razor views (.cshtml)
- Entity Framework Core
- HTML and CSS
- Git and GitHub
- Visual Studio

## Requirements

To open and run the project, install:

- Visual Studio 2022 with support for .NET 8
- The ASP.NET and web development workload
- The .NET 8 SDK
- The database software required by the project's configuration

## Getting Started

### 1. Download the project

On this repository's GitHub page:

1. Click Code.
2. Select Download ZIP.
3. Extract the downloaded ZIP file.

Alternatively, clone the repository using Git.

### 2. Open the solution

1. Open Visual Studio.
2. Select Open a project or solution.
3. Locate and select Staybnb.sln.
4. Wait for Visual Studio to load the solution and restore its packages.

### 3. Configure the database

Check the application's configuration and database setup before running it.

- Use a connection string suitable for your local database.
- Store private local credentials using Visual Studio's Manage User Secrets feature.
- Never commit passwords or private connection strings to GitHub.

If the database is set up using the project's Entity Framework Core migrations:

1. Open Tools → NuGet Package Manager → Package Manager Console.
2. Select the project containing the database context.
3. Run:

   Update-Database

The database connection must be configured correctly before this command can succeed.

### 4. Build the application

In Visual Studio, select:

Build → Build Solution

Resolve any configuration or build errors before continuing.

### 5. Run the application

1. Set the Staybnb web project as the startup project if necessary.
2. Select its configured launch profile.
3. Press F5, or Ctrl + F5 to run without debugging.
4. Wait for the application to open in your browser.

## Project Structure

```text
Repository
├── Staybnb.sln          Solution file
├── Staybnb/             Application source code
│   ├── Models/          Application data models
│   ├── Views/           Razor user-interface pages
│   └── wwwroot/         Static web assets
├── .gitignore           Files excluded from Git
└── README.md            Project documentation
