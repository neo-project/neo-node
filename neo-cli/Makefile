#
# Copyright (C) 2015-2021 The neo project. All rights reserved.
#
# Permission is hereby granted, free of charge, to any person
# obtaining a copy of this software and associated documentation
# files (the "Software"), to deal in the Software without restriction,
# including without limitation the rights to use, copy, modify,
# merge, publish, distribute, sublicense, and/or sell copies of the
# Software, and to permit persons to whom the Software is furnished
# to do so, subject to the following conditions:
#
# The above copyright notice and this permission notice shall be
# included in all copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
# EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
# OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
# IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
# ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
# CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
# WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
#
#

ifeq ($(DOTNET),)
	export DOTNET=/usr/local/share/dotnet/dotnet
endif

DOTNET_PATH ?= $(DOTNET)
Build_Mode = Release
output_dir = output
Build_Network = MAIN_NETWORK
DOCKER = docker


.PHONY: all main test dev test publish clean

ifeq ($(DEBUG), 1)
	Build_Mode = Debug
else ifeq ($(RELEASE), 1)
	Build_Mode = Release
endif

ifneq ($(OUTDIR),)
	$(shell mkdir -p $(OUTDIR))
	output_dir = $(OUTDIR)
endif

define download_plugin
	-@test -f $(output_dir)/Plugins/$(1).dll || (curl -s https://api.github.com/repos/neo-project/neo-modules/releases/latest | grep $(1).zip | cut -d : -f 2,3 | tr -d \" | wget -i -)
	-@test -f $(1).zip && (unzip -q $(1).zip -d $(output_dir) && rm -f *.zip*)
endef

define build_main
	$(call download_plugin,LevelDBStore)
	@cp ./config/config.json $(output_dir)/config/config.json
    @rm -f *.zip*
endef

define build_test
	$(call download_plugin,LevelDBStore)
	$(call download_plugin,RpcServer)
	@rm -f $(output_dir)/config.json
	@cp ./config/config.testnet.json $(output_dir)/config/config.json
	@rm -f *.zip*
endef

define build_dev
	$(call download_plugin,LevelDBStore)
	$(call download_plugin,RpcServer)
	$(call download_plugin,ApplicationLogs)
	$(call download_plugin,DBFTPlugin)
	@rm -f $(output_dir)/config.json
	@cp ./config/config.devnet.json $(output_dir)/config/config.json
	@rm -f *.zip*
endef

ifeq ($(NETWORK), MAIN)
	$(call build_main)
else ifeq ($(NETWORK), TEST)
	Build_Network = TEST_NETWORK
	$(call build_test)
else ifeq ($(NETWORK), DEV)
	Build_Network = DEV_NETWORK
	$(call build_dev)
else
	$(call build_main)
endif

all:
	$(DOTNET) build --configuration $(Build_Mode) --output $(output_dir)
	@echo "The project has been built in $(Build_Mode) mode on $(Build_Network)."

main_net:
	$(DOTNET) build --configuration Release --output $(output_dir)
	$(call build_main)
	@echo "The project has been built on main net."

test_net:
	$(DOTNET) build --configuration Release --output $(output_dir)
	$(call build_test)
	@echo "The project has been built on test net."

dev_net:
	$(DOTNET) build --configuration Debug --output $(output_dir)
	$(call build_dev)
	@echo "The project has been built on private net."

docker:
	$(DOCKER) build -t neo-cli .

run_docker:
	$(DOCKER) run -p 10332:10332 -p 10333:10333 --name=neo-cli-mainnet neo-cli
	$(DOCKER) exec -it neo-cli-mainnet /bin/bash

run:
	$(DOTNET) $(output_dir)/neo-cli.dll

test:
	$(DOTNET) test

publish:main

clean:
	@rm -rf $(output_dir)
	@rm -f *.zip*
