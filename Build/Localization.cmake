include_guard(GLOBAL)

find_program(SPELLJAMMER_DOTNET_EXECUTABLE dotnet REQUIRED)
set(SPELLJAMMER_ROOT "${CMAKE_CURRENT_LIST_DIR}/..")

if(CMAKE_BUILD_TYPE)
    set(SPELLJAMMER_DOTNET_CONFIGURATION ${CMAKE_BUILD_TYPE})
else()
    set(SPELLJAMMER_DOTNET_CONFIGURATION Debug)
endif()

add_custom_target(SpelljammerLocalizationRuntime
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} build
        ${SPELLJAMMER_ROOT}/Source/Spelljammer.Localization/Spelljammer.Localization.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        -p:RestoreIgnoreFailedSources=true
    WORKING_DIRECTORY ${SPELLJAMMER_ROOT}
    COMMENT "Building the Spelljammer localization runtime"
    VERBATIM
)

add_custom_target(SpelljammerLocalizationCompiler
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} build
        ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        -p:RestoreIgnoreFailedSources=true
    WORKING_DIRECTORY ${SPELLJAMMER_ROOT}
    COMMENT "Building the Spelljammer localization compiler"
    VERBATIM
)
add_dependencies(SpelljammerLocalizationCompiler SpelljammerLocalizationRuntime)

add_custom_target(SpelljammerLocalizationCatalogs
    COMMAND ${CMAKE_COMMAND} -E make_directory
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US
    COMMAND ${CMAKE_COMMAND} -E make_directory
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/fr-FR
    COMMAND ${CMAKE_COMMAND} -E make_directory
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/zh-Hant-TW
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/core.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/core.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/menu.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/menu.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/settings.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/settings.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/creation.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/creation.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/fr-FR/menu.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/fr-FR/menu.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/fr-FR/settings.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/fr-FR/settings.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/fr-FR/creation.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/fr-FR/creation.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/zh-Hant-TW/menu.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/zh-Hant-TW/menu.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/zh-Hant-TW/settings.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/zh-Hant-TW/settings.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/zh-Hant-TW/creation.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/zh-Hant-TW/creation.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/content-pack.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/content-pack.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/attributes.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/attributes.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/skills.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/skills.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/accesses.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/accesses.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/backgrounds.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/backgrounds.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/feats.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/feats.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/trainings.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/trainings.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/techniques.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/techniques.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/spells.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/spells.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/psychic-techniques.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/psychic-techniques.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/races.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/races.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/heritages.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/heritages.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/perks.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/perks.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/commands.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/commands.sfloc
   COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/characters.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/characters.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/equipment.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/equipment.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/cells.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/cells.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/links.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/links.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/boards.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/boards.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/encounters.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/encounters.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/frames.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/frames.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/modules.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/modules.sfloc
    COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
        ${SPELLJAMMER_DOTNET_EXECUTABLE} run
        --project ${SPELLJAMMER_ROOT}/Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj
        --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
        -p:Platform=AnyCPU
        --no-build
        -- compile
        ${SPELLJAMMER_ROOT}/Content/Packs/base/Localization/en-US/ship-weapons.sfloc.json
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/ship-weapons.sfloc
    BYPRODUCTS
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/accesses.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/backgrounds.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/feats.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/trainings.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/techniques.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/spells.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/psychic-techniques.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/races.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/heritages.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/perks.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/commands.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/core.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/menu.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/settings.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/creation.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/fr-FR/menu.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/fr-FR/settings.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/fr-FR/creation.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/zh-Hant-TW/menu.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/zh-Hant-TW/settings.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/zh-Hant-TW/creation.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/content-pack.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/attributes.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/skills.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/characters.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/equipment.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/cells.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/links.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/boards.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/encounters.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/frames.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/modules.sfloc
        ${CMAKE_CURRENT_BINARY_DIR}/Localization/en-US/ship-weapons.sfloc
    WORKING_DIRECTORY ${SPELLJAMMER_ROOT}
    COMMENT "Compiling deterministic Spelljammer localization catalogs"
    VERBATIM
)
add_dependencies(SpelljammerLocalizationCatalogs SpelljammerLocalizationCompiler)

if(BUILD_TESTING)
    add_custom_target(SpelljammerLocalizationUnitTests
        COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
            ${SPELLJAMMER_DOTNET_EXECUTABLE} build
            ${SPELLJAMMER_ROOT}/Tests/Spelljammer.Localization.Tests/Spelljammer.Localization.Tests.csproj
            --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
            -p:Platform=AnyCPU
            -p:RestoreIgnoreFailedSources=true
        WORKING_DIRECTORY ${SPELLJAMMER_ROOT}
        COMMENT "Compiling the Spelljammer localization unit-test target (execution is CI-owned)"
        VERBATIM
    )
    add_dependencies(SpelljammerLocalizationUnitTests SpelljammerLocalizationCatalogs)

    add_test(NAME SpelljammerLocalizationTests
        COMMAND ${SPELLJAMMER_DOTNET_EXECUTABLE} run
            --project ${SPELLJAMMER_ROOT}/Tests/Spelljammer.Localization.Tests/Spelljammer.Localization.Tests.csproj
            --configuration ${SPELLJAMMER_DOTNET_CONFIGURATION}
            -p:Platform=AnyCPU
            --no-build
            --no-restore
    )
    set_tests_properties(SpelljammerLocalizationTests PROPERTIES
        LABELS SpelljammerLocalizationUnit
    )
endif()
