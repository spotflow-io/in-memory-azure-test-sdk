# Contributing guidelines

By contributing to `In-Memory Azure Test SDKs`, you declare that:

* You are entitled to assign the copyright for the work, provided it is not owned by your employer or you have received a written copyright assignment.
* You license your contribution under the same terms that apply to the rest of the `In-Memory Azure Test SDKs` project.
* You pledge to follow the [Code of Conduct](./CODE_OF_CONDUCT.md).

## Contribution process

Please, always create an [Issue](https://github.com/spotflow-io/in-memory-azure-test-sdk/issues/new) before starting to work on a new feature or bug fix. This way, we can discuss the best approach and avoid duplicated or lost work. Without discussing the issue first, there is a risk that your PR will not be accepted because e.g.:

* It does not fit the project's goals.
* It is not implemented in the way that we would like to see.
* It is already being worked on by someone else.

### Commits & Pull Requests

We do not put any specific requirements on individual commits. However, we expect that the Pull Request (PR) is a logical unit of work that is easily understandable & reviewable. The PRs should also contain expressive title and description.

Few general rules are:

* Do not mix multiple unrelated changes in a single PR.
* Do not mix formatting changes with functional changes.
* Do not mix refactoring with functional changes.
* Do not create huge PRs that are hard to review. In case that your change is logically cohesive but still large, consider splitting it into multiple PRs.

### Code style

This project generally follows usual code style for .NET projects as described in [Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/). In situations where Guideline is not clear, applicable or strict adherence to the Guideline obviously causes more harm than good, deviating from the Guideline is acceptable.

We use `dotnet-format` to format the code. Code formatting is checked in the CI pipeline and failing to pass the formatting check will result in a failed build.

### Testing

All new code must be covered by tests. Prefer adding new tests specific for the new code, but feel free to extend existing tests if it makes sense.

### Documentation & changelog

All new features and changes must be reflected in the documentation [README.md](./README.md) and/or [docs](./docs). Also, make sure to update the [CHANGELOG.md](./CHANGELOG.md) file with a brief description of the changes. The changelog follows the [Keep a Changelog](https://keepachangelog.com) format.

## Architecture of in-memory clients

#### General rules

* No static state should be used in the in-memory clients.

#### Client constructors

* The in-memory clients should have constructors that are as similar to the real clients as possible.
* In-general, the constructors should omit authentication-related parameters. If the parameters are needed for constructor signature to be unique, these parameters can be included, but no-op/dummy implementation of the related type must be provided. (e.g. [NoOpTokenCredential](./src/Spotflow.InMemory.Azure/Auth/NoOpTokenCredential.cs)).

#### Supported client methods & properties

* If a supported method accepts a parameter that is related to a unsupported feature, the parameter should be ignored and the method call should not fail because of it. Such unsupported feature should be explicitly documented in the table of supported and unsupported features.
* No supported method should not return dummy constant value. Instead, they should return a value that reflects the current state of the in-memory provider.

##### Async methods

* All supported methods must also support their async counterparts if they exist in the real client.  
* All supported methods must be truly async (currently, this is mostly ensured by calling `Task.Yield()` at the start of each method).
  * The `Task.Yield()` should be present as soon as in the callstack as possible.
  * Do not use `ConfigureAwait(ConfigureAwaitOptions.ForceYielding)` instead for consistency sake.
* `ConfigureAwait(false)` or `ConfigureAwait(ConfigureAwaitOptions)` are not used. The problems associated with this decision are not considered to be insignificant for this library.

#### Unsupported client methods & properties

* All unsupported methods should be explicitly overwritten and throw `NotSupportedException` exception. This way, user is not exposed to confusing behavior coming from inherited real clients which are (by design) not properly configured.

#### Thread-safety

* Clients must be thread-safe.

#### In-memory providers & related types

* Root in-memory provider should be always public and should have a public parameterless constructor if possible.
* Visibility of related types representing the current in-memory state should be determined by how the concepts that the types represent are usually managed in Azure:
  * If the concept mostly managed via Azure management-plane, the related type should be public and should expose relevant properties and methods to manage the concept. E.g. [InMemoryEventHub](./src/Spotflow.InMemory.Azure.EventHubs/InMemoryEventHub.cs).
  * If the concept mostly managed via Azure data-plane, the related type should be internal. E.g. [InMemoryBlockBlob](./src/Spotflow.InMemory.Azure.Storage/Blobs/Internals/InMemoryBlockBlob.cs).