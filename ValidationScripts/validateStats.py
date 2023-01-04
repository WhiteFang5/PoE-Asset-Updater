import json
import io

fileName = 'stats'

def writeLog(logFile, log):
    print(log.encode("utf-8"))
    logFile.write(log + "\n")

#open stats files
with open(fileName + '.json', 'r', encoding='utf-8') as f:
    originalStats = json.load(f)
with open('newFiles/' + fileName + '.json', 'r', encoding='utf-8') as f:
    newStats = json.load(f)

with io.open('validate-' + fileName + '.log', 'w', encoding='utf-8') as f:
    # check which data got removed or changed
    for originalType in originalStats:
        #writeLog(f, f'Checking {originalId}')
        if originalType not in newStats:
            writeLog(f, f'Missing {originalType}')
        else:
            for originalTradeId in originalStats[originalType]:
                originalLang = '1'
                if originalTradeId not in newStats[originalType]:
                    originalTrade = originalStats[originalType][originalTradeId]
                    originalKey = 'text'
                    if originalKey in originalTrade and originalLang in originalTrade[originalKey]:
                        originalTextObj = originalStats[originalType][originalTradeId][originalKey][originalLang][0]
                        writeLog(f, f'Missing {originalType}.{originalTradeId} (Value: {originalTextObj[list(originalTextObj)[0]]})')
                    else:
                        writeLog(f, f'Missing {originalType}.{originalTradeId}')
                else:
                    for originalKey in originalStats[originalType][originalTradeId]:
                        if originalKey not in newStats[originalType][originalTradeId]:
                            if originalKey != 'text':
                                writeLog(f, f'Missing {originalType}.{originalTradeId}.{originalKey} (Value: {originalStats[originalType][originalTradeId][originalKey]})')
                            else:
                                if originalLang in originalStats[originalType][originalTradeId][originalKey]:
                                    originalTextObj = originalStats[originalType][originalTradeId][originalKey][originalLang][0]
                                    writeLog(f, f'Missing {originalType}.{originalTradeId}.{originalKey} (Value: {originalTextObj[list(originalTextObj)[0]]})')
                                else:
                                    writeLog(f, f'Missing {originalType}.{originalTradeId}.{originalKey}')
                        elif originalKey == 'text':
                            if originalLang not in originalStats[originalType][originalTradeId][originalKey]:
                                writeLog(f, f'Missing {originalType}.{originalTradeId}.{originalKey}.{originalLang}')
                            elif originalLang not in newStats[originalType][originalTradeId][originalKey]:
                                writeLog(f, f'Missing {originalType}.{originalTradeId}.{originalKey}.{originalLang}')
                            else:
                                for originalTextIdx, originalTextObj in enumerate(originalStats[originalType][originalTradeId][originalKey][originalLang]):
                                    newLang = newStats[originalType][originalTradeId][originalKey][originalLang]
                                    for originalTextKey in originalTextObj:
                                        originalTextValue = originalTextObj[originalTextKey]
                                        originalTextFound = False
                                        if originalTextIdx < len(newLang):
                                            newTextObj = newLang[originalTextIdx]
                                            if originalTextKey in newTextObj:
                                                originalTextFound = True
                                                newTextValue = newTextObj[originalTextKey]
                                                if originalTextValue != newTextValue:
                                                    writeLog(f, f'Changed {originalType}.{originalTradeId}.{originalKey}.{originalLang}[{originalTextIdx}].{originalTextKey} Value (Original: {originalTextValue} | New: {newTextValue})')
                                        if not originalTextFound:
                                            for newTextIdx, newTextObj in enumerate(newLang):
                                                if originalTextKey in newTextObj:
                                                    originalTextFound = True
                                                    newTextValue = newTextObj[originalTextKey]
                                                    if originalTextValue != newTextValue:
                                                        writeLog(f, f'Changed {originalType}.{originalTradeId}.{originalKey}.{originalLang}[{originalTextIdx}].{originalTextKey} Value (Original: {originalTextValue} | New: {newTextValue})')
                                                    if originalTextIdx != newTextIdx:
                                                        writeLog(f, f'Changed {originalType}.{originalTradeId}.{originalKey}.{originalLang}[{originalTextIdx}].{originalTextKey} Idx (Original: {originalTextIdx} | New: {newTextIdx}) (Value: {originalTextValue})')
                                                    break
                                            if not originalTextFound:
                                                for newTextIdx, newTextObj in enumerate(newLang):
                                                    for newTextKey in newTextObj:
                                                        newTextValue = newTextObj[newTextKey]
                                                        if originalTextValue == newTextValue:
                                                            originalTextFound = True
                                                            writeLog(f, f'Changed {originalType}.{originalTradeId}.{originalKey}.{originalLang}[{originalTextIdx}].{originalTextKey} Key (Original: {originalTextKey} | New: {newTextKey}) (Value: {originalTextValue})')
                                                            if originalTextIdx != newTextIdx:
                                                                writeLog(f, f'Changed {originalType}.{originalTradeId}.{originalKey}.{originalLang}[{originalTextIdx}].{originalTextKey} Idx (Original: {originalTextIdx} | New: {newTextIdx}) (Value: {originalTextValue})')
                                                            break
                                                    if originalTextFound:
                                                        break
                                        if not originalTextFound:
                                            writeLog(f, f'Missing {originalType}.{originalTradeId}.{originalKey}.{originalLang}[{originalTextIdx}].{originalTextKey} (Value: {originalTextValue})')
                        else:
                            originalValue = str(originalStats[originalType][originalTradeId][originalKey])
                            newValue = str(newStats[originalType][originalTradeId][originalKey])
                            if originalValue != newValue:
                                writeLog(f, f'Changed {originalType}.{originalTradeId}.{originalKey} (Original: {originalValue} | New: {newValue})')

    # check if which new data got added
    for newType in newStats:
        if newType not in originalStats:
           writeLog(f, f'Added {newType}')
        else:
            for newTradeId in newStats[newType]:
                if newTradeId not in originalStats[newType]:
                    newEnglishStatNames = newStats[newType][newTradeId]['text']['1'][0]
                    newEnglish = newEnglishStatNames[list(newEnglishStatNames.keys())[0]]
                    writeLog(f, f'Added {newType}.{newTradeId} (Value: {newEnglish})')
                else:
                    for newKey in newStats[newType][newTradeId]:
                        if newKey not in originalStats[newType][newTradeId]:
                            writeLog(f, f'Added {newType}.{newTradeId}.{newKey}')
                        elif newKey == 'text':
                            for newLang in newStats[newType][newTradeId][newKey]:
                                if newLang not in originalStats[newType][newTradeId][newKey]:
                                    writeLog(f, f'Added {newType}.{newTradeId}.{newKey}.{newLang}')
                                else:
                                    for newTextIdx, newTextObj in enumerate(newStats[newType][newTradeId][newKey][newLang]):
                                        originalLang = originalStats[newType][newTradeId][newKey][newLang]
                                        if newTextIdx >= len(originalLang):
                                            writeLog(f, f'Added {newType}.{newTradeId}.{newKey}.{newLang}[{newTextIdx}]')
                                            for newTextKey in newTextObj:
                                                newTextValue = newTextObj[newTextKey]
                                                writeLog(f, f'Added {newType}.{newTradeId}.{newKey}.{newLang}[{newTextIdx}].{newTextKey} (Value: {newTextValue})')
                                        else:
                                            originalTextObj = originalLang[newTextIdx]
                                            for newTextKey in newTextObj:
                                                newTextValue = newTextObj[newTextKey]
                                                if not newTextKey in originalTextObj:
                                                    originalTextValueFound = False
                                                    for originalTextKey in originalTextObj:
                                                        originalTextValue = originalTextObj[originalTextKey]
                                                        if newTextValue == originalTextValue:
                                                            originalTextValueFound = True
                                                            break
                                                    if not originalTextValueFound:
                                                        writeLog(f, f'Added {newType}.{newTradeId}.{newKey}.{newLang}[{newTextIdx}].{newTextKey} (Value: {newTextValue})')
