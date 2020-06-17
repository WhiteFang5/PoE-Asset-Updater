import json
import io

fileName = 'stats'

def writeLog(logFile, log):
    print(log)
    logFile.write(log + "\n")

def checkExplicitStats(logFile, statKey, ogStat, newStat):
    idMismatch = False
    for key in ogStat:
        if key not in newStat:
            writeLog(logFile, statKey + ' is missing key: ' + key)
        else:
            originalWord = str(ogStat[key])
            newWord = str(newStat[key])
            if key != 'text' and originalWord != newWord:
                writeLog(logFile, statKey + ' has mismatched: ' + key + ' (Original: ' + originalWord + ' | New: ' + newWord + ')')
                if key == 'id':
                    idMismatch = True
            elif idMismatch is False and key == 'text' and '1' in ogStat[key]:
                for desc in ogStat[key]['1']:
                    if desc not in newStat[key]['1']:
                        writeLog(logFile, statKey + ' is missing desc ' + desc + ' (Value: ' + ogStat[key]['1'][desc])
                    else:
                        originalDesc = ogStat[key]['1'][desc]
                        newDesc = newStat[key]['1'][desc]
                        if originalDesc != newDesc:
                            writeLog(logFile, statKey + ' has mismatched desc: ' + desc + ' (Original: ' + originalDesc + ' | New: ' + newDesc + ')')

#open stats files
with open(fileName + '.json', 'r', encoding='utf-8') as f:
    originalStats = json.load(f)
with open('newFiles/' + fileName + '.json', 'r', encoding='utf-8') as f:
    newStats = json.load(f)

with io.open('validate-' + fileName + '.log', 'w', encoding='utf-8') as f:
    for statType in originalStats:
        writeLog(f, 'Checking stat type: ' + statType)
        if statType not in newStats:
            # check for stat type keys
            writeLog(f, 'Missing: ' + statType)
        else: 
            # checks that all stat_x types match in explicit, implicit, etc
            for stat_x in originalStats[statType]:
                if stat_x not in newStats[statType]:
                    if 'id' in originalStats[statType]:
                        writeLog(f, 'Missing stat: ' + stat_x + ' (ID: ' + originalStats[statType]['id'] + ')')
                    else:
                        writeLog(f, 'Missing stat: ' + stat_x + ' (No ID)')
                else:
                    checkExplicitStats(f, stat_x, originalStats[statType][stat_x], newStats[statType][stat_x])
        writeLog(f, '---')
        writeLog(f, '')