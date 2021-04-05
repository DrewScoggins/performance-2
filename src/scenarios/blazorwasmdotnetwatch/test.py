'''
C# Console app
'''
from shared.runner import TestTraits, Runner
import os

EXENAME = 'blazorwasmdotnetwatch'

if __name__ == "__main__":
    traits = TestTraits(exename=EXENAME, 
                        startupmetric='DotnetWatch',
                        guiapp='false',
                        innerloopcommandargs='-c "from shutil import copyfile; copyfile(\'src%sPages%sCounter.razor\', \'app%sPages%sCounter.razor\')"' % (os.sep, os.sep, os.sep, os.sep) ,
                        projext = '.csproj',
                        processwillexit='false',
                        measurementdelay='20',
<<<<<<< HEAD
                        iterations='5',
                        runwithoutexit='true'
=======
                        iterations='3'
>>>>>>> 6d63bdfb... Add dotnet watch with hot reload scenario
                        )
    Runner(traits).run()
