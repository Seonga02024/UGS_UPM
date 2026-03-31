// Cloud Code JS: PLAYER_MONEY 조회 + 보정
// Rule:
// - min_money (Remote Config, long)
// - free_money (Remote Config, long)
// PLAYER_MONEY가 없거나 min_money 이하이면 free_money로 저장 후 반환

const { DataApi } = require("@unity-services/cloud-save-1.4");
const { SettingsApi } = require("@unity-services/remote-config-1.1");

function tryParseJson(value) {
  if (typeof value !== "string") {
    return value;
  }

  try {
    return JSON.parse(value);
  } catch (e) {
    return value;
  }
}

function findDeepByKey(obj, targetKey) {
  if (!obj || typeof obj !== "object") {
    return undefined;
  }

  const targetKeyLower = String(targetKey).toLowerCase();

  if (Object.prototype.hasOwnProperty.call(obj, targetKey)) {
    return obj[targetKey];
  }

  for (const ownKey of Object.keys(obj)) {
    if (String(ownKey).toLowerCase() === targetKeyLower) {
      return obj[ownKey];
    }
  }

  const idLike =
    (Object.prototype.hasOwnProperty.call(obj, "key") && obj.key) ||
    (Object.prototype.hasOwnProperty.call(obj, "id") && obj.id) ||
    (Object.prototype.hasOwnProperty.call(obj, "name") && obj.name);
  if (idLike !== undefined && String(idLike).toLowerCase() === targetKeyLower) {
    if (Object.prototype.hasOwnProperty.call(obj, "value")) {
      return obj.value;
    }
    return obj;
  }

  if (Array.isArray(obj)) {
    for (const item of obj) {
      const found = findDeepByKey(item, targetKey);
      if (found !== undefined) {
        return found;
      }
    }
    return undefined;
  }

  for (const key of Object.keys(obj)) {
    const found = findDeepByKey(obj[key], targetKey);
    if (found !== undefined) {
      return found;
    }
  }

  return undefined;
}

async function loadMoneyConfig(remoteConfigApi, projectId, environmentId) {
  const assigned = await remoteConfigApi.assignSettingsGet(
    projectId,
    environmentId,
    "settings",
    ["min_money", "free_money"]
  );

  const assignedData = assigned?.data ?? {};
  const minRaw = tryParseJson(findDeepByKey(assignedData, "min_money"));
  const freeRaw = tryParseJson(findDeepByKey(assignedData, "free_money"));

  const minMoney = Number(minRaw);
  const freeMoney = Number(freeRaw);

  if (!Number.isFinite(minMoney) || !Number.isFinite(freeMoney)) {
    throw new Error("Remote Config keys min_money/free_money are missing or invalid.");
  }

  return { minMoney, freeMoney };
}

module.exports = async ({ params, context, logger }) => {
  const { projectId, accessToken, environmentId } = context;
  const playerId = params?.playerId || context.playerId;
  const KEY = "PLAYER_MONEY";

  if (!playerId) {
    throw new Error("playerId is required.");
  }

  const cloudSaveApi = new DataApi(context);
  const remoteConfigApi = new SettingsApi({ accessToken });

  try {
    const { minMoney, freeMoney } = await loadMoneyConfig(remoteConfigApi, projectId, environmentId);

    const getRes = await cloudSaveApi.getItems(projectId, playerId, KEY);
    const found = getRes?.data?.results?.[0]?.value;

    let currentMoney = Number(found);
    const hasValue = found !== undefined && found !== null && !Number.isNaN(currentMoney);

    let updated = false;
    if (!hasValue || currentMoney <= minMoney) {
      currentMoney = freeMoney;

      await cloudSaveApi.setItem(projectId, playerId, {
        key: KEY,
        value: String(currentMoney),
      });

      updated = true;
    }

    return {
      success: true,
      playerId,
      key: KEY,
      money: currentMoney,
      updated,
      message: updated
        ? `PLAYER_MONEY was missing or <= ${minMoney}, so it was set to ${freeMoney}.`
        : "PLAYER_MONEY loaded without changes.",
    };
  } catch (err) {
    logger.error(`[GetOrInitPlayerMoney] playerId=${playerId} error=${err.message}`);
    return {
      success: false,
      playerId,
      key: KEY,
      money: 0,
      updated: false,
      message: err.message,
    };
  }
};
