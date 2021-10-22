#
# Copyright (C) 2015-2021 The neo project. All rights reserved.
#
# Redistribution and use in source and binary forms, with or without
# modification, are permitted provided that the following conditions
# are met:
#
#   * Redistributions of source code must retain the above copyright
#     notice, this list of conditions and the following disclaimer.
#   * Redistributions in binary form must reproduce the above copyright
#     notice, this list of conditions and the following disclaimer in
#     the documentation and/or other materials provided with the
#     distribution.
#   * Neither the name of Intel Corporation nor the names of its
#     contributors may be used to endorse or promote products derived
#     from this software without specific prior written permission.
#
# THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
# "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
# LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
# A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
# OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
# SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
# LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
# DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
# THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
# (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
# OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#
#

ifeq ($(DOTNET),)
	export DOTNET=/usr/local/share/dotnet/dotnet
endif

DOTNET_PATH ?= $(DOTNET)
Build_Mode = Release
output_dir = output
Build_Network = MAIN_NETWORK

.PHONY: all main test dev publish clean

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
    | wget -qi -

	@unzip $(1).zip -d $(output_dir)/
	@rm -f $(1).*
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
	@echo "The project has been built on main net."

test:
	$(DOTNET) build --configuration Release --output $(output_dir)
	@rm -f $(output_dir)/config.json
	@cp config.testnet.json $(output_dir)/config.json
	$(call build_test)
	@echo "The project has been built on test net."

dev:
	$(DOTNET) build --configuration Debug --output $(output_dir)
	@rm -f $(output_dir)/config.json
	@cp config.devnet.json $(output_dir)/config.json
	$(call build_dev)
	@echo "The project has been built on private net."

publish:main

clean:
	@rm -rf $(output_dir)
