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
                if originalTradeId not in newStats[originalType]:
                    writeLog(f, f'Missing {originalType}.{originalTradeId}')
                else:
                    for originalKey in originalStats[originalType][originalTradeId]:
                        if originalKey not in newStats[originalType][originalTradeId]:
                            if originalKey != 'text':
                                writeLog(f, f'Missing {originalType}.{originalTradeId}.{originalKey} (Value: {originalStats[originalType][originalTradeId][originalKey]})')
                            else:
                                writeLog(f, f'Missing {originalType}.{originalTradeId}.{originalKey}')
                        elif originalKey == 'text':
                            originalLang = '1'
                            if originalLang not in originalStats[originalType][originalTradeId][originalKey]:
                                writeLog(f, f'Missing {originalType}.{originalTradeId}.{originalKey}.{originalLang}')
                            elif originalLang not in newStats[originalType][originalTradeId][originalKey]:
                                writeLog(f, f'Missing {originalType}.{originalTradeId}.{originalKey}.{originalLang}')
                            else:
                                for originalTextObj in originalStats[originalType][originalTradeId][originalKey][originalLang]:
                                    newLang = newStats[originalType][originalTradeId][originalKey][originalLang]
                                    for originalText in originalTextObj:
                                        originalTextValue = originalTextObj[originalText]
                                        exists = False
                                        existsKeyMatch = False
                                        existsNewText = None
                                        for newTextObj in newLang:
                                            for newText in newTextObj:
                                                if originalTextValue == newTextObj[newText]:
                                                    if not existsKeyMatch:
                                                        existsNewText = newText
                                                    existsKeyMatch |= (originalText == existsNewText)
                                                    exists = True
                                        if not exists:
                                            writeLog(f, f'Missing {originalType}.{originalTradeId}.{originalKey}.{originalLang}[].{originalText} (Value: {originalTextValue})')
                                        elif originalText != existsNewText:
                                            writeLog(f, f'Changed {originalType}.{originalTradeId}.{originalKey}.{originalLang}[].{originalText} (Original: {originalText} | New {existsNewText})')
                        else:
                            originalValue = str(originalStats[originalType][originalTradeId][originalKey])
                            newValue = str(newStats[originalType][originalTradeId][originalKey])
                            if originalValue != newValue:
                                writeLog(f, f'Changed {originalType}.{originalTradeId}.{originalKey} (Original: {originalValue} | New {newValue})')

    # check if which new data got added
    for newType in newStats:
        if newType not in originalStats:
           writeLog(f, f'Added {newType}')
        else:
            for newTradeId in newStats[newType]:
                if newTradeId not in originalStats[newType]:
                    writeLog(f, f'Added {newType}.{newTradeId}')
                else:
                    for newKey in newStats[newType][newTradeId]:
                        if newKey not in originalStats[newType][newTradeId]:
                            writeLog(f, f'Added {newType}.{newTradeId}.{newKey}')
                        elif newKey == 'text':
                            for newLang in newStats[newType][newTradeId][newKey]:
                                if newLang not in originalStats[newType][newTradeId][newKey]:
                                    writeLog(f, f'Added {newType}.{newTradeId}.{newKey}.{newLang}')
                                else:
                                    for newTextObj in newStats[newType][newTradeId][newKey][newLang]:
                                        originalLang = originalStats[newType][newTradeId][newKey][newLang]
                                        for newText in newTextObj:
                                            newTextValue = newTextObj[newText]
                                            exists = False
                                            for originalTextObj in originalLang:
                                                for originalText in originalTextObj:
                                                    if newTextValue == originalTextObj[originalText]:
                                                        exists = True
                                                        break
                                                if exists:
                                                    break
                                            if not exists:
                                                writeLog(f, f'Added {newType}.{newTradeId}.{newKey}.{newLang}[].{newText} (Value: {newTextValue})')
