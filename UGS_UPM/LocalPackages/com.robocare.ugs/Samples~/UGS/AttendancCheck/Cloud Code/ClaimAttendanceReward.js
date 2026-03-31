// Cloud Code JS
// Endpoint name: ClaimAttendanceReward
//
// params: {}
 
const { DataApi } = require("@unity-services/cloud-save-1.4");
const { SettingsApi } = require("@unity-services/remote-config-1.1");

const PLAYER_MONEY_KEY = "PLAYER_MONEY";
const ATTENDANCE_STATE_KEY = "ATTENDANCE_STATE";
const KST_OFFSET_MS = 9 * 60 * 60 * 1000;

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

  // Case-insensitive direct key match
  for (const ownKey of Object.keys(obj)) {
    if (String(ownKey).toLowerCase() === targetKeyLower) {
      return obj[ownKey];
    }
  }

  // Some Remote Config payloads use entry objects:
  // { key: "ATTENDANCE_REWARDS", value: {...} }
  // { id: "ATTENDANCE_REWARDS", value: {...} }
  // { name: "ATTENDANCE_REWARDS", value: {...} }
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

function extractAttendanceRewards(assignedData) {
  const settings = assignedData?.configs?.settings;
  let raw;

  // Known Remote Config response shapes
  if (settings && typeof settings === "object") {
    if (Array.isArray(settings)) {
      const entry = settings.find((x) => {
        const idLike = x?.key ?? x?.id ?? x?.name;
        return idLike !== undefined && String(idLike).toLowerCase() === "attendance_rewards";
      });
      if (entry) {
        raw = entry?.value ?? entry;
      }
    } else {
      for (const k of Object.keys(settings)) {
        if (String(k).toLowerCase() === "attendance_rewards") {
          raw = settings[k];
          break;
        }
      }
    }
  }

  if (raw === undefined) {
    raw = findDeepByKey(assignedData, "ATTENDANCE_REWARDS");
  }

  const parsed = tryParseJson(raw);

  return {
    raw,
    parsed,
    found: raw !== undefined,
    parsedIsObject: !!(parsed && typeof parsed === "object"),
  };
}

function fail(
  errorCode,
  currentMoney = 0,
  rewardDay = "",
  reward = 0,
  claimCount = 0,
  monthKey = ""
) {
  return {
    success: false,
    errorCode,
    currentMoney,
    rewardDay,
    reward,
    claimCount,
    monthKey,
  };
}

function toKstDate(utcDate) {
  return new Date(utcDate.getTime() + KST_OFFSET_MS);
}

function formatMonthKey(kstDate) {
  const y = kstDate.getUTCFullYear();
  const m = String(kstDate.getUTCMonth() + 1).padStart(2, "0");
  return `${y}-${m}`;
}

function formatDateKey(kstDate) {
  const y = kstDate.getUTCFullYear();
  const m = String(kstDate.getUTCMonth() + 1).padStart(2, "0");
  const d = String(kstDate.getUTCDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

function normalizeAttendanceState(rawValue) {
  const parsed = tryParseJson(rawValue);
  if (!parsed || typeof parsed !== "object") {
    return {
      monthKey: "",
      lastClaimAt: "",
      claimCount: 0,
    };
  }

  const claimCountParsed = Number(parsed.claimCount);
  return {
    monthKey: String(parsed.monthKey ?? ""),
    lastClaimAt: String(parsed.lastClaimAt ?? ""),
    claimCount: Number.isNaN(claimCountParsed) || claimCountParsed < 0 ? 0 : claimCountParsed,
  };
}

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId: callerPlayerId, accessToken, environmentId } = context;

  const playerId = String(context?.playerId ?? "").trim();
  const nowUtc = new Date();
  const nowKst = toKstDate(nowUtc);
  const currentMonthKey = formatMonthKey(nowKst);
  const todayKey = formatDateKey(nowKst);

  if (!playerId) {
    return fail("INVALID_PARAMS");
  }

  if (callerPlayerId && callerPlayerId !== playerId) {
    return fail("FORBIDDEN");
  }

  const cloudSaveApi = new DataApi(context);
  const remoteConfigApi = new SettingsApi({ accessToken });
 
  try {
    const moneyRes = await cloudSaveApi.getItems(projectId, playerId, PLAYER_MONEY_KEY);
    const cloudMoneyRaw = moneyRes?.data?.results?.[0]?.value;
    const cloudMoney = Number(cloudMoneyRaw);

    if (Number.isNaN(cloudMoney)) {
      return fail("MONEY_NOT_FOUND");
    }

    const attendanceRes = await cloudSaveApi.getItems(projectId, playerId, ATTENDANCE_STATE_KEY);
    const attendanceRaw = attendanceRes?.data?.results?.[0]?.value;
    let attendanceState = normalizeAttendanceState(attendanceRaw);

    if (attendanceState.monthKey !== currentMonthKey) {
      attendanceState = {
        monthKey: currentMonthKey,
        lastClaimAt: "",
        claimCount: 0,
      };
    }

    if (attendanceState.lastClaimAt) {
      const lastClaimUtc = new Date(attendanceState.lastClaimAt);
      if (!Number.isNaN(lastClaimUtc.getTime())) {
        const lastClaimKst = toKstDate(lastClaimUtc);
        if (formatDateKey(lastClaimKst) === todayKey) {
          return fail(
            "ALREADY_CLAIMED_TODAY",
            cloudMoney,
            String(attendanceState.claimCount),
            0,
            attendanceState.claimCount,
            attendanceState.monthKey
          );
        }
      }
    }

    const assigned = await remoteConfigApi.assignSettingsGet(
      projectId,
      environmentId,
      "settings",
      ["ATTENDANCE_REWARDS"]
    );

    const assignedData = assigned?.data ?? {};
    const rcExtract = extractAttendanceRewards(assignedData);

    if (!rcExtract.found) {
      const configsRaw = assignedData?.configs;
      const settingsRaw = configsRaw?.settings;
      const configsIsArray = Array.isArray(configsRaw);
      const configsIsObject = !!configsRaw && typeof configsRaw === "object" && !configsIsArray;
      const configsArray = configsIsArray ? configsRaw : [];
      const configsPreview = configsArray.slice(0, 3).map((c) => ({
        keys: Object.keys(c || {}),
        keyField: c?.key ?? null,
        idField: c?.id ?? null,
        nameField: c?.name ?? null,
        typeField: c?.type ?? null,
      }));

      return fail("ATTENDANCE_CONFIG_NOT_FOUND", cloudMoney, "", 0, attendanceState.claimCount, attendanceState.monthKey, {
        projectId,
        environmentId: environmentId ?? null,
        hasAssignedData: !!assignedData,
        assignedDataKeys: Object.keys(assignedData),
        configsType: typeof configsRaw,
        configsIsArray,
        configsIsObject,
        configsObjectKeys: configsIsObject ? Object.keys(configsRaw) : [],
        configsCount: configsArray.length,
        configsPreview,
        settingsType: typeof settingsRaw,
        settingsIsArray: Array.isArray(settingsRaw),
        settingsIsObject: !!settingsRaw && typeof settingsRaw === "object",
        settingsKeys:
          settingsRaw && typeof settingsRaw === "object" && !Array.isArray(settingsRaw)
            ? Object.keys(settingsRaw)
            : [],
      });
    }

    if (!rcExtract.parsedIsObject) {
      return fail("ATTENDANCE_CONFIG_PARSE_FAILED", cloudMoney, "", 0, attendanceState.claimCount, attendanceState.monthKey, {
        rawType: typeof rcExtract.raw,
      });
    }

    const dayRewards = rcExtract.parsed?.day_rewards;
    if (dayRewards === undefined) {
      return fail("DAY_REWARDS_NOT_FOUND", cloudMoney, "", 0, attendanceState.claimCount, attendanceState.monthKey, {
        configKeys: Object.keys(rcExtract.parsed || {}),
      });
    }

    if (!Array.isArray(dayRewards)) {
      return fail("DAY_REWARDS_NOT_ARRAY", cloudMoney, "", 0, attendanceState.claimCount, attendanceState.monthKey, {
        dayRewardsType: typeof dayRewards,
      });
    }

    const nextClaimCount = attendanceState.claimCount + 1;
    const rewardDay = String(nextClaimCount);
    const rewardEntry = dayRewards.find((x) => String(x?.id) === rewardDay);
    if (!rewardEntry) {
      return fail("MONTH_REWARD_COMPLETED", cloudMoney, "", 0, attendanceState.claimCount, attendanceState.monthKey);
    }

    const reward = Number(rewardEntry.reward);
    if (Number.isNaN(reward) || reward < 0) {
      return fail("REWARD_VALUE_INVALID", cloudMoney, rewardDay, 0, attendanceState.claimCount, attendanceState.monthKey);
    }

    const updatedMoney = cloudMoney + reward;
    const updatedState = {
      monthKey: currentMonthKey,
      lastClaimAt: nowUtc.toISOString(),
      claimCount: nextClaimCount,
    };

    await Promise.all([
      cloudSaveApi.setItem(projectId, playerId, {
        key: PLAYER_MONEY_KEY,
        value: String(updatedMoney),
      }),
      cloudSaveApi.setItem(projectId, playerId, {
        key: ATTENDANCE_STATE_KEY,
        value: updatedState,
      }),
    ]);

    return {
      success: true,
      errorCode: null,
      currentMoney: updatedMoney,
      rewardDay,
      reward,
      claimCount: nextClaimCount,
      monthKey: currentMonthKey,
    };
  } catch (e) {
    logger.error(`[ClaimAttendanceReward] ${e.message}`);
    return fail("INTERNAL_ERROR");
  }
};
