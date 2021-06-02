import json
import io

fileName = 'base-item-types-v2'

def writeLog(logFile, log):
    print(log.encode("utf-8"))
    logFile.write(log + "\n")

#open stats files
with open(fileName + '.json', 'r', encoding='utf-8') as f:
    originalBITs = json.load(f)
with open('newFiles/' + fileName + '.json', 'r', encoding='utf-8') as f:
    newBITs = json.load(f)

with io.open('validate-' + fileName + '.log', 'w', encoding='utf-8') as f:
    # check which data got removed or changed
    for originalId in originalBITs:
        #writeLog(f, f'Checking {originalId}')
        # check if original exists in new
        if originalId not in newBITs:
            writeLog(f, f'Missing {originalId}')
        else: 
            # check all keys
            for originalKey in originalBITs[originalId]:
                if originalKey not in newBITs[originalId]:
                    writeLog(f, f'Missing {originalId}.{originalKey}')
                else:
                    if originalKey == 'names':
                        for originalName in originalBITs[originalId][originalKey]:
                            if originalName not in newBITs[originalId][originalKey]:
                                writeLog(f, f'Missing {originalId}.{originalName}')
                            else:
                                originalNameValue = originalBITs[originalId][originalKey][originalName]
                                newNameValue = newBITs[originalId][originalKey][originalName]
                                if originalNameValue != newNameValue:
                                    writeLog(f, f'Changed {originalId}.{originalKey}.{originalName} (Original: {originalNameValue} | New {newNameValue})')
                    else:
                        originalValue = str(originalBITs[originalId][originalKey])
                        newValue = str(newBITs[originalId][originalKey])
                        if originalValue != newValue:
                            writeLog(f, f'Changed {originalId}.{originalKey} (Original: {originalValue} | New: {newValue})')
        #writeLog(f, '---')
        #writeLog(f, '')

    # check if which new data got added
    for newId in newBITs:
        if newId not in originalBITs:
           writeLog(f, f'Added {newId}')
        else:
           for newKey in newBITs[newId]:
               if newKey not in originalBITs[newId]:
                   writeLog(f, f'Added {newId}.{newKey}')
