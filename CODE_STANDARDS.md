# Team Coding Rules

## Naming conventions
 - Use camelCase for local variables and method parameters.
 - Use PascalCase for classes, methods, and public members.
 - Names should be descriptive and meaningful.

## Code organization
 - Keep methods short and focused on one task (aim for under 40–50 lines).
 - Each class should follow the Single Responsibility Principle.
 - Organize files into logical folders (e.g., controllers, models, services).

## Documentation
 - Write XML/inline comments for all public methods and properties.
 - Add comments for any complex or non-obvious logic.
 - Write clear commit messages that explain the purpose of the change.

## Error handling
 - Use specific exception handling rather than catching general exceptions.
 - Provide useful error messages and log errors when needed.
 - Avoid silent failures—errors should be visible and traceable.

## Testing
 - Unit tests should be written for all public methods.
 - The team goal is at least 80% code coverage.
 - All tests must pass before merging into the main branch.
