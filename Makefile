UNITY ?= /Applications/Unity/Hub/Editor/6000.4.7f1/Unity.app/Contents/MacOS/Unity
STATE_PY ?= $(HOME)/Desktop/poicasi-org/tools/design-state/state.py
REPO ?= $(CURDIR)
GAME ?= warukyure
UI_STATE_LOG := design-state/_raw/unity.log
TEST_RESULTS := design-state/_raw/test_results.xml

.PHONY: ui-state
ui-state:
	mkdir -p design-state/_raw
	"$(UNITY)" -batchmode -projectPath "$(REPO)" -runTests -testPlatform PlayMode -testFilter "UIStateCapture" -testResults "$(TEST_RESULTS)" -logFile "$(UI_STATE_LOG)"
	python3 "$(STATE_PY)" annotate --game $(GAME) --repo "$(REPO)"
	python3 "$(STATE_PY)" verify --game $(GAME) --repo "$(REPO)"
