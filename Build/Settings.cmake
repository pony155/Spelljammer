include_guard(GLOBAL)

find_program(SPELLJAMMER_DOTNET_EXECUTABLE dotnet REQUIRED)
set(SPELLJAMMER_ROOT "${CMAKE_CURRENT_LIST_DIR}/..")

if(CMAKE_BUILD_TYPE)
    set(SPELLJAMMER_DOTNET_CONFIGURATION ${CMAKE_BUILD_TYPE})
else()
    set(SPELLJAMMER_DOTNET_CONFIGURATION Debug)
endif()

add_custom_target(SpelljammerSettingsRuntime
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} build
        ${SPELLJAMMER_ROOT}/Source/Spelljammer.Settings/Spelljammer.Settings.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        -p:RestoreIgnoreFailedSources=true
    WORKING_DIRECTORY ${SPELLJAMMER_ROOT}
    COMMENT "Building the Spelljammer game-settings runtime"
    VERBATIM
)

if(BUILD_TESTING)
    add_custom_target(SpelljammerSettingsUnitTests
        COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
            ${SPELLJAMMER_DOTNET_EXECUTABLE} build
            ${SPELLJAMMER_ROOT}/Tests/Spelljammer.Settings.Tests/Spelljammer.Settings.Tests.csproj
            --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
            -p:Platform=AnyCPU
            -p:RestoreIgnoreFailedSources=true
        WORKING_DIRECTORY ${SPELLJAMMER_ROOT}
        COMMENT "Compiling the Spelljammer game-settings contract target (execution is CI-owned)"
        VERBATIM
    )
    add_dependencies(SpelljammerSettingsUnitTests SpelljammerSettingsRuntime)

    add_test(NAME SpelljammerSettingsTests
        COMMAND ${SPELLJAMMER_DOTNET_EXECUTABLE} run
            --project ${SPELLJAMMER_ROOT}/Tests/Spelljammer.Settings.Tests/Spelljammer.Settings.Tests.csproj
            --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
            -p:Platform=AnyCPU
            --no-build
            --no-restore
    )
    set_tests_properties(SpelljammerSettingsTests PROPERTIES
        LABELS SpelljammerSettingsUnit
    )
endif()
