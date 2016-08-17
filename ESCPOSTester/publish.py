from xml.dom import minidom
from subprocess import check_output
import os

# GUID: 99211924-9BC1-4A3A-B661-948D3C1A6347

#Set this to the Visual Studio output directory
path = 'P:\ESCPOSTester'
app = "ESCPOSTester"


#Path to Mage.exe
mage = '"C:\Program Files (x86)\Microsoft SDKs\Windows\\v7.0A\Bin\mage.exe"'

# Path to Signtool.exe
signtool = '"C:\Program Files (x86)\Microsoft SDKs\Windows\\v7.1A\Bin\signtool.exe"'

#SHA 1 Hash (even though this is a SHA2 cert)
hash = os.environ['EV_CERT_ID']

#Timestamp URL
timestamp = 'http://timestamp.digicert.com'

#Acquire the current version's manifest - Stupid, stupid, stupid, stupid
xmldoc = minidom.parse(path+"\\"+app+'.application')
itemlist = xmldoc.getElementsByTagName('dependentAssembly') 
manifest = path + "\\" + itemlist[0].attributes['codebase'].value

#Get to the publish directory
setupst = '{} sign /sha1 {} /t "{}" /v "{}\\setup.exe"'.format(signtool,hash,timestamp,path)
manifst = '{} -sign "{}" -ch {} -ti "{}"'.format(mage,manifest,hash,timestamp)
applica = '{} -update "{}{}{}.application" -appmanifest "{}" -ch {} -ti "{}"'.format(mage,path,"\\",app,manifest,hash,timestamp)

#print(setupst)
#print(manifst)
#print(applica)

check_output(setupst, shell=True)
check_output(manifst, shell=True)
check_output(applica, shell=True)

