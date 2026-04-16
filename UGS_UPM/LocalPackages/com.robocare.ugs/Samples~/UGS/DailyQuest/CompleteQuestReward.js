const { DataApi } = require("@unity-services/cloud-save-1.4");
const { SettingsApi } = require("@unity-services/remote-config-1.1");

const PLAYER_MONEY_KEY = "PLAYER_MONEY";
const QUEST_DEFINITIONS_KEY = "QUEST_DEFINITIONS";
const MAX_RETRY = 3;

module.exports = async ({ params, context, logger }) => {
  const api = new DataApi(context);
  const questId = (params && params.questId) || "";

  if (!questId) {
    return buildResponse(false, "INVALID_QUEST_ID", questId, 0, 0, false, "questId is required");
  }

  try {
    const definitions = await loadQuestDefinitions(context);
    const dailyQuests = Array.isArray(definitions && definitions.daily_quests) ? definitions.daily_quests : [];
    const matched = dailyQuests.find((q) => q && q.id === questId);

    if (!matched) {
      return buildResponse(false, "QUEST_NOT_FOUND", questId, 0, 0, false, "Quest id not found in QUEST_DEFINITIONS");
    }

    const reward = Number(matched.reward);
    if (!Number.isFinite(reward) || reward < 0) {
      return buildResponse(false, "INVALID_REWARD", questId, 0, 0, false, "Reward is invalid");
    }

    for (let i = 0; i < MAX_RETRY; i += 1) {
      try {
        const current = await readMoney(api, context.projectId, context.playerId);
        const nextMoney = current.money + reward;

        await api.setItem(context.projectId, context.playerId, {
          key: PLAYER_MONEY_KEY,
          value: nextMoney,
          writeLock: current.writeLock,
        });

        return buildResponse(true, "", questId, reward, nextMoney, true, "OK");
      } catch (error) {
        const code = Number(error && (error.status || error.statusCode));
        const isWriteConflict = code === 409 || code === 412;
        if (isWriteConflict && i < MAX_RETRY - 1) {
          logger && logger.warn && logger.warn(`[CompleteQuestReward] write conflict retry=${i + 1}`);
          continue;
        }

        logger && logger.error && logger.error(`[CompleteQuestReward] cloud save failed: ${error.message}`);
        return buildResponse(false, "CLOUD_SAVE_ERROR", questId, 0, 0, false, error.message || "cloud save failed");
      }
    }

    return buildResponse(false, "UNKNOWN_ERROR", questId, 0, 0, false, "unexpected retry failure");
  } catch (error) {
    logger && logger.error && logger.error(`[CompleteQuestReward] failed: ${error.message}`);
    return buildResponse(false, "REMOTE_CONFIG_ERROR", questId, 0, 0, false, error.message || "remote config failed");
  }
};

async function loadQuestDefinitions(context) {
  const settingsApi = new SettingsApi({ accessToken: context.accessToken });

  const response = await settingsApi.assignSettingsGet(
    context.projectId,
    context.environmentId,
    "settings",
    [QUEST_DEFINITIONS_KEY]
  );

  const data = (response && response.data) || response || {};
  const raw = extractConfigValue(data, QUEST_DEFINITIONS_KEY);

  if (!raw) {
    throw new Error("QUEST_DEFINITIONS is missing");
  }

  return typeof raw === "string" ? JSON.parse(raw) : raw;
}

function extractConfigValue(root, targetKey) {
  if (!root || typeof root !== "object") {
    return null;
  }

  if (Object.prototype.hasOwnProperty.call(root, targetKey)) {
    return root[targetKey];
  }

  if (Array.isArray(root)) {
    const kv = root.find((x) => x && x.key === targetKey);
    if (kv && Object.prototype.hasOwnProperty.call(kv, "value")) {
      return kv.value;
    }

    for (const item of root) {
      const found = extractConfigValue(item, targetKey);
      if (found !== null && found !== undefined) {
        return found;
      }
    }
    return null;
  }

  const containers = [
    root.configs,
    root.settings,
    root.values,
    root.items,
    root.results,
    root.metadata,
  ];

  for (const c of containers) {
    const found = extractConfigValue(c, targetKey);
    if (found !== null && found !== undefined) {
      return found;
    }
  }

  for (const key of Object.keys(root)) {
    const val = root[key];
    if (val && typeof val === "object") {
      const found = extractConfigValue(val, targetKey);
      if (found !== null && found !== undefined) {
        return found;
      }
    }
  }

  return null;
}

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

function buildResponse(success, errorCode, questId, reward, currentMoney, updated, message) {
  return {
    success,
    errorCode,
    questId,
    reward,
    currentMoney,
    updated,
    message,
  };
}
