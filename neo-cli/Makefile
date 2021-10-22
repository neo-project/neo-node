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
	-curl -s https://api.github.com/repos/neo-project/neo-modules/releases/latest \
    | grep $(1).zip \
    | cut -d : -f 2,3 \
    | tr -d \" \
    | wget -i -
	unzip $(1).zip -d $(output_dir)/
	rm -f $(1).*
endef

define build_main
	$(call download_plugin,LevelDBStore)
endef

define build_test
	$(call download_plugin,LevelDBStore)
	$(call download_plugin,RpcServer)
endef

define build_dev
	$(call download_plugin,LevelDBStore)
	$(call download_plugin,RpcServer)
	$(call download_plugin,ApplicationLogs)
	$(call download_plugin,DBFTPlugin)
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

main:
	$(DOTNET) build --configuration Release --output $(output_dir)
	$(call build_main)
	@cp ./config/config.json $(output_dir)/config.json
	@echo "The project has been built on main net."

test:
	$(DOTNET) build --configuration Release --output $(output_dir)
	@rm -f $(output_dir)/config.json
	@cp ./config/config.testnet.json $(output_dir)/config.json
	$(call build_test)
	@echo "The project has been built on test net."

dev:
	$(DOTNET) build --configuration Debug --output $(output_dir)
	@rm -f $(output_dir)/config.json
	@cp ./config/config.devnet.json $(output_dir)/config.json
	@cp ./config/devnet.json $(output_dir)/devnet.json
	$(call build_dev)
	@echo "The project has been built on private net."

run:
	$(DOTNET) $(output_dir)/neo-cli.dll

test:
	$(DOTNET) test

publish:main

clean:
	@rm -rf $(output_dir)
	@rm -f *.zip*
