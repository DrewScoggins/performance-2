'''
pre-command
'''
import sys, os, subprocess, shutil
from performance.logger import setup_loggers
from shared import const
from shared.precommands import PreCommands
from test import EXENAME

# For blazor3.2 the linker argument '--dump-dependencies' should be added statically to enable linker dump
# For blazor5.0 the linker argument can be added to the command line as an msbuild property
setup_loggers(True)
precommands = PreCommands()
precommands.new(template='blazorwasm',
                output_dir=const.APPDIR,
                bin_dir=const.BINDIR,
                exename=EXENAME,
                working_directory=sys.path[0])
subprocess.run(["dotnet", "new", "tool-manifest", "--force"])
subprocess.run(["dotnet", "tool", "install", "dotnet-install-blazoraot", "--version", "6.0.0-preview*"])
subprocess.run(["dotnet", "install-blazoraot"])
f = open(os.path.join(os.getcwd(), "app", "emptyblazorwasmtemplate.csproj"), 'r')
outFileText = ""
for line in f.readlines():
    if "<PropertyGroup>" in line:
        outFileText += line
        outFileText += "    <RunAOTCompilation>true</RunAOTCompilation>" + os.linesep
    else:
        outFileText += line
os.remove(os.path.join(os.getcwd(), "app", "emptyblazorwasmtemplate.csproj"))
f = open(os.path.join(os.getcwd(), "app", "emptyblazorwasmtemplate.csproj"), 'w')
f.write(outFileText)
f.close()
precommands.execute()
