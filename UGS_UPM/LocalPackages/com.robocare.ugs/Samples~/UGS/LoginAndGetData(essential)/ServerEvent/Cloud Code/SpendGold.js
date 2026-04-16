const { DataApi } = require("@unity-services/cloud-save-1.4");

const PLAYER_MONEY_KEY = "PLAYER_MONEY";
const MAX_RETRY = 3;

module.exports = async ({ params, context, logger }) => {
  const api = new DataApi(context);
  const playerId = (params && params.playerId) || context.playerId;
  const amount = Number(params && params.amount);

  if (!playerId) {
    return buildResponse(false, playerId, 0, false, "PLAYER_ID_REQUIRED");
  }

  if (!Number.isFinite(amount) || amount <= 0 || !Number.isInteger(amount)) {
    return buildResponse(false, playerId, 0, false, "INVALID_AMOUNT");
  }

  for (let i = 0; i < MAX_RETRY; i += 1) {
    try {
      const current = await readMoney(api, context.projectId, playerId);

      if (current.money < amount) {
        return buildResponse(false, current.money, "INSUFFICIENT_FUNDS");
      }

      const nextMoney = current.money - amount;

      await api.setItem(context.projectId, playerId, {
        key: PLAYER_MONEY_KEY,
        value: nextMoney,
        writeLock: current.writeLock,
      });

      return buildResponse(true, nextMoney, "OK");
    } catch (error) {
      const code = Number(error && (error.status || error.statusCode));
      const isWriteConflict = code === 409 || code === 412;
      if (isWriteConflict && i < MAX_RETRY - 1) {
        logger && logger.warn && logger.warn(`[SpendGold] write conflict retry=${i + 1}`);
        continue;
      }

      logger && logger.error && logger.error(`[SpendGold] failed: ${error.message}`);
      return buildResponse(false, 0, `CLOUD_SAVE_ERROR:${error.message}`);
    }
  }

  return buildResponse(false, 0, "UNKNOWN_ERROR");
};

async function readMoney(api, projectId, playerId) {
  const response = await api.getItems(projectId, playerId, [PLAYER_MONEY_KEY]);
  const results =
    (response && response.data && response.data.results) ||
    (response && response.results) ||
    [];

  const found = Array.isArray(results)
    ? results.find((item) => item && item.key === PLAYER_MONEY_KEY)
    : null;

  const money = found && Number.isFinite(Number(found.value)) ? Number(found.value) : 0;
  const writeLock = found ? found.writeLock : null;

  return { money, writeLock };
}

function buildResponse(success, money, message) {
  return {
    success,
    gold: money,
    message,
  };
}
