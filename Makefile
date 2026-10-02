.DEFAULT_GOAL := build
GAME_PATH ?= $(NIVALIS_GAME_PATH)
DOTNET ?= $(if $(wildcard ../.tools/dotnet/dotnet),../.tools/dotnet/dotnet,dotnet)
export DOTNET_CLI_TELEMETRY_OPTOUT := 1
export DOTNET_CLI_HOME := $(CURDIR)/.tools/cli-home
export NUGET_PACKAGES := $(CURDIR)/.tools/nuget
export GAME_PATH DOTNET RELEASE_TAG
.PHONY: build test install package deploy check-release help
build:
	@test -n "$(GAME_PATH)" || { echo 'Set GAME_PATH to the game directory.'; exit 1; }
	@"$(DOTNET)" build src/ModCompanion.csproj -c Release "-p:GamePath=$(GAME_PATH)" --nologo
test:
	@"$(DOTNET)" run --project tests/Checks.csproj -c Release "-p:GamePath=$(GAME_PATH)"
install: build
	@install -Dm644 bin/Nivalis.ModCompanion.dll "$(GAME_PATH)/BepInEx/plugins/Nivalis.ModCompanion.dll"

package: build test
	@python3 tools/package.py

# Deployment requires a clean checkout; missing version tags are created after verification.
deploy:
	@bash tools/deploy.sh

check-release:
	@bash tools/check_release.sh

help:
	@printf '%s\n' 'make build GAME_PATH="..."    Build the DLL' 'make test GAME_PATH="..."     Run API checks' 'make package GAME_PATH="..."  Build, test and create the ZIP' 'make install GAME_PATH="..."  Build and install (close the game first)' 'make deploy GAME_PATH="..."   Build, test, tag if needed and publish a GitHub release' 'make check-release RELEASE_TAG=1.0.0  Check version consistency'
