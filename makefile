
SOURCE_DIR = src
PROJECT_NAME = dnsserver
ROOT_FILE_NAME = projectroot.fsx
DEPLOY_PATH = F:\_mp

SOURCE_FILE_NAME = $(SOURCE_DIR)/$(ROOT_FILE_NAME)

EXE_FILE_NAME = $(PROJECT_NAME).exe
EXE_FILE = out/$(EXE_FILE_NAME)

CMD = Fsc
OPTIONS = 
REFERENCES = --reference:System.Runtime.Caching.dll

build:
	@echo Compiling...
	@$(CMD) $(OPTIONS) $(SOURCE_FILE_NAME) -o $(EXE_FILE) --nologo $(REFERENCES)
	@echo Compiled.

# Send the file into a folder indexed by PATH
deploy:
	@cp "$(EXE_FILE)" "$(DEPLOY_PATH)\$(EXE_FILE_NAME)"
	@echo Deployed.
