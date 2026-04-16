const { DataApi } = require("@unity-services/cloud-save-1.4");
const { SettingsApi } = require("@unity-services/remote-config-1.1");

const PLAYER_MONEY_KEY = "PLAYER_MONEY";
const GAME_REWARDS_KEY = "GAME_REWARDS";
const MAX_RETRY = 3;

module.exports = async ({ params, context, logger }) => {
  const api = new DataApi(context);
  const rewardId = (params && params.rewardId) || "";

  if (!rewardId) {
    return buildResponse(false, "INVALID_REWARD_ID", rewardId, 0, 0, false, "rewardId is required");
  }

  try {
    const definitions = await loadGameRewards(context);
    const rewards = Array.isArray(definitions && definitions.stage_rewards) ? definitions.stage_rewards : [];
    const matched = rewards.find((r) => r && r.id === rewardId);

    if (!matched) {
      return buildResponse(false, "REWARD_NOT_FOUND", rewardId, 0, 0, false, "Reward id not found in GAME_REWARDS");
    }

    const reward = Number(matched.reward);
    if (!Number.isFinite(reward) || reward < 0) {
      return buildResponse(false, "INVALID_REWARD", rewardId, 0, 0, false, "Reward is invalid");
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

        return buildResponse(true, "", rewardId, reward, nextMoney, true, "OK");
      } catch (error) {
        const code = Number(error && (error.status || error.statusCode));
        const isWriteConflict = code === 409 || code === 412;
        if (isWriteConflict && i < MAX_RETRY - 1) {
          logger && logger.warn && logger.warn(`[CompleteGameReward] write conflict retry=${i + 1}`);
          continue;
        }

        logger && logger.error && logger.error(`[CompleteGameReward] cloud save failed: ${error.message}`);
        return buildResponse(false, "CLOUD_SAVE_ERROR", rewardId, 0, 0, false, error.message || "cloud save failed");
      }
    }

    return buildResponse(false, "UNKNOWN_ERROR", rewardId, 0, 0, false, "unexpected retry failure");
  } catch (error) {
    logger && logger.error && logger.error(`[CompleteGameReward] failed: ${error.message}`);
    return buildResponse(false, "REMOTE_CONFIG_ERROR", rewardId, 0, 0, false, error.message || "remote config failed");
  }
};

async function loadGameRewards(context) {
  const settingsApi = new SettingsApi({ accessToken: context.accessToken });
  const response = await settingsApi.assignSettingsGet(
    context.projectId,
    context.environmentId,
    "settings",
    [GAME_REWARDS_KEY]
  );

  const data = (response && response.data) || response || {};
  const raw = extractConfigValue(data, GAME_REWARDS_KEY);

  if (!raw) {
    throw new Error("GAME_REWARDS is missing");
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

  // Common shape: [{ key: "...", value: ... }]
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

function buildResponse(success, errorCode, rewardId, reward, currentMoney, updated, message) {
  return {
    success,
    errorCode,
    rewardId,
    reward,
    currentMoney,
    updated,
    message,
  };
}
