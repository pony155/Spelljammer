include_guard(GLOBAL)

find_program(SPELLJAMMER_DOTNET_EXECUTABLE dotnet REQUIRED)
set(SPELLJAMMER_ROOT "${CMAKE_CURRENT_LIST_DIR}/..")

if(CMAKE_BUILD_TYPE)
    set(SPELLJAMMER_DOTNET_CONFIGURATION ${CMAKE_BUILD_TYPE})
else()
    set(SPELLJAMMER_DOTNET_CONFIGURATION Debug)
endif()

add_custom_target(SpelljammerContentRuntime
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} build
        ${SPELLJAMMER_ROOT}/Source/Spelljammer.Content/Spelljammer.Content.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        -p:RestoreIgnoreFailedSources=true
    WORKING_DIRECTORY ${SPELLJAMMER_ROOT}
    COMMENT "Building the Spelljammer content runtime"
    VERBATIM
)
add_dependencies(SpelljammerContentRuntime SpelljammerSimulationRuntime)

add_custom_target(SpelljammerContentCompiler
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} build
        ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Content.Compiler/Spelljammer.Content.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        -p:RestoreIgnoreFailedSources=true
    WORKING_DIRECTORY ${SPELLJAMMER_ROOT}
    COMMENT "Building the Spelljammer offline content validator"
    VERBATIM
)
add_dependencies(SpelljammerContentCompiler SpelljammerContentRuntime)

add_custom_target(SpelljammerContentPacks
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Content.Compiler/Spelljammer.Content.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- validate
        ${SPELLJAMMER_ROOT}/Content/Packs/base
    WORKING_DIRECTORY ${SPELLJAMMER_ROOT}
    COMMENT "Validating deterministic Spelljammer gameplay content"
    VERBATIM
)
add_dependencies(SpelljammerContentPacks SpelljammerContentCompiler)

if(BUILD_TESTING)
    add_custom_target(SpelljammerContentUnitTests
        COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
            ${SPELLJAMMER_DOTNET_EXECUTABLE} build
            ${SPELLJAMMER_ROOT}/Tests/Spelljammer.Content.Tests/Spelljammer.Content.Tests.csproj
            --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
            -p:Platform=AnyCPU
            -p:RestoreIgnoreFailedSources=true
        WORKING_DIRECTORY ${SPELLJAMMER_ROOT}
        COMMENT "Compiling the Spelljammer content contract target (execution is CI-owned)"
        VERBATIM
    )
    add_dependencies(SpelljammerContentUnitTests SpelljammerContentPacks)

    add_test(NAME SpelljammerContentTests
        COMMAND ${SPELLJAMMER_DOTNET_EXECUTABLE} run
            --project ${SPELLJAMMER_ROOT}/Tests/Spelljammer.Content.Tests/Spelljammer.Content.Tests.csproj
            --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
            -p:Platform=AnyCPU
            --no-build
            --no-restore
    )
    set_tests_properties(SpelljammerContentTests PROPERTIES
        LABELS SpelljammerContentUnit
    )
endif()
