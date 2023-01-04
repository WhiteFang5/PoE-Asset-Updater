import json
import io

fileName = 'stats'

def writeLog(logFile, log):
    print(log)
    logFile.write(log + "\n")

#open stats files
with open(fileName + '.json', 'r', encoding='utf-8') as f:
    stats = json.load(f)

with io.open('validate-identical-' + fileName + '.log', 'w', encoding='utf-8') as f:
    for statType in stats:
        writeLog(f, 'Checking stat type: ' + statType)
        stat_type_x = stats[statType]
        for stat_x in stat_type_x:
            if 'id' in stat_type_x[stat_x] and 'text' in stat_type_x[stat_x] and '1' in stat_type_x[stat_x]['text']:
                descs = stat_type_x[stat_x]['text']['1']
                mod = ''
                if 'mod' in stat_type_x[stat_x]:
                    mod = stat_type_x[stat_x]['mod']
                for desc in descs:
                    descContent = descs[desc]
                    for stat_y in stat_type_x:
                        if stat_x != stat_y and 'id' in stat_type_x[stat_y] and 'text' in stat_type_x[stat_y] and '1' in stat_type_x[stat_y]['text']:
                            otherDescs = stat_type_x[stat_y]['text']['1']
                            otherMod = ''
                            if 'mod' in stat_type_x[stat_y]:
                                otherMod = stat_type_x[stat_y]['mod']
                            for otherDesc in otherDescs:
                                otherDescContent = otherDescs[otherDesc]
                                if descContent == otherDescContent:
                                    modLog = 'ML: ' + str(mod == otherMod)
                                    if mod != '':
                                        modLog += ' Mod=' + mod
                                    if otherMod != '':
                                        modLog += ' OtherMod=' + otherMod
                                    writeLog(f, 'Identical Stat: ' + stat_x + ' and ' + stat_y + ' | ' + modLog + ' | Desc: \'' + descContent + '\'')
        writeLog(f, '---')
        writeLog(f, '')