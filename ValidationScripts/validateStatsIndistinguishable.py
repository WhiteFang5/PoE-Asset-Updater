import json
import io

fileName = 'stats-indistinguishable'

def writeLog(logFile, log):
	print(log.encode("utf-8"))
	logFile.write(log + "\n")

#open stats files
with open(fileName + '.json', 'r', encoding='utf-8') as f:
	originalStats = json.load(f)['indistinguishableStats']
with open('newFiles/' + fileName + '.json', 'r', encoding='utf-8') as f:
	newStats = json.load(f)['indistinguishableStats']

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
					for originalTradeIdListItem in originalStats[originalType][originalTradeId]:
						if originalTradeIdListItem not in newStats[originalType][originalTradeId]:
							writeLog(f, f'Missing {originalType}.{originalTradeId}.[].{originalTradeIdListItem}')

	# check if which new data got added
	for newType in newStats:
		if newType not in originalStats:
		   writeLog(f, f'Added {newType}')
		else:
			for newTradeId in newStats[newType]:
				if newTradeId not in originalStats[newType]:
					writeLog(f, f'Added {newType}.{newTradeId}')
				else:
					for newTradeIdListItem in newStats[newType][newTradeId]:
						if newTradeIdListItem not in originalStats[newType][newTradeId]:
							writeLog(f, f'Added {newType}.{newTradeId}.[].{newTradeIdListItem}')
