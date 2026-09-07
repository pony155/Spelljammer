# Build integration

The repository root is a standalone CMake project for Game-owned tooling. The
root `CMakeLists.txt` enables CTest and includes the focused simulation,
content, and localization declarations.

`Simulation.cmake` declares these targets:

| Target | Purpose |
| --- | --- |
| `SpelljammerSimulationRuntime` | Compile the headless space-expedition simulation. |
| `SpelljammerSimulationUnitTests` | Compile, but do not execute, the deterministic simulation contract program. |

`Localization.cmake` declares these targets:

| Target | Purpose |
| --- | --- |
| `SpelljammerLocalizationRuntime` | Compile the localization runtime assembly. |
| `SpelljammerLocalizationCompiler` | Compile the source-catalog compiler. |
| `SpelljammerLocalizationCatalogs` | Compile the authored base `en-US` catalogs. |
| `SpelljammerLocalizationUnitTests` | Compile, but do not execute, the test program. |

`Content.cmake` declares these targets:

| Target | Purpose |
| --- | --- |
| `SpelljammerContentRuntime` | Compile the bounded gameplay content runtime. |
| `SpelljammerContentCompiler` | Compile the offline content validator and registry reporter. |
| `SpelljammerContentPacks` | Validate the built-in gameplay content pack. |
| `SpelljammerContentUnitTests` | Compile, but do not execute, the content contract program. |

Configure and compile runtime test targets from the repository root:

```powershell
cmake -S . -B cmake-build-debug -G Ninja -DCMAKE_BUILD_TYPE=Debug
cmake --build cmake-build-debug --target SpelljammerSimulationUnitTests
cmake --build cmake-build-debug --target SpelljammerLocalizationUnitTests
cmake --build cmake-build-debug --target SpelljammerContentUnitTests
```

Unit-test execution remains user/CI-owned under the repository policy.
