import json
import io

fileName = 'base-item-type-categories'

def writeLog(logFile, log):
    print(log.encode("utf-8"))
    logFile.write(log + "\n")

#open stats files
with open(fileName + '.json', 'r', encoding='utf-8') as f:
    originalStats = json.load(f)
with open('newFiles/' + fileName + '.json', 'r', encoding='utf-8') as f:
    newStats = json.load(f)

with io.open('validate-' + fileName + '.log', 'w', encoding='utf-8') as f:
    for language in originalStats:
        writeLog(f, 'Checking language type: ' + language)
        if language not in newStats:
            # check for stat type keys
            writeLog(f, 'Missing: ' + language)
        else: 
            # checks that all stat_x types match in explicit, implicit, etc
            for word in originalStats[language]:
                if word not in newStats[language]:
                    writeLog(f, 'Missing word: ' + word)
                else:
                    originalWord = originalStats[language][word]
                    newWord = newStats[language][word]
                    if originalWord != newWord:
                        writeLog(f, 'Incorrect translation for: ' + language + ' ' + word + ' (Original: ' + originalWord + ' | New: ' + newWord + ')')
        writeLog(f, '---')
        writeLog(f, '')