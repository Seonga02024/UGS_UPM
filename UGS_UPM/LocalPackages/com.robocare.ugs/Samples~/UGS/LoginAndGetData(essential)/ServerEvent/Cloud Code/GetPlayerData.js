// 다른 프로젝트로부터 받아오는 공통 재화 스크립트
// 부모 프로젝트에 따라서 PROJECT_A_ID / ENV_ID / BASIC_AUTH 바꿔야 함 
const axios = require('axios');

module.exports = async ({ params, context, logger }) => {
  const PROJECT_A_ID = "7ff917e1-60d7-4963-9970-77894ea9ef95";
  const ENV_ID = "7e69ca9d-9de2-4804-8454-d4f7fa64650e"; //
  const PLAYER_ID = context.playerId;
  const BASIC_AUTH = "Basic NjFlMzliMDQtMzM5ZC00Yjg0LTkyMTEtZDRjZjVjMTc3MWQwOlZMN1oxUXhMS0J3Sk50U0dqVjhfWjN5OWRlVXVWTmx2";

  try {

    // 1. 토큰 교환
    const tokenResponse = await axios({
      method: 'post',
      url: `https://services.api.unity.com/auth/v1/token-exchange?projectId=${PROJECT_A_ID}&environmentId=${ENV_ID}`,
      headers: { 
        'Authorization': BASIC_AUTH,
        'Content-Type': 'application/json', // 415 에러 해결을 위해 명시
        'Accept': 'application/json'
      },
      data: {} // 빈 데이터라도 보내야 Content-Type이 유지됩니다.
    });

    const accessToken = tokenResponse.data.accessToken;

    if (params.action === "GET_PLAYER_GOLD_DATA") {
      try{
        const targetId = params.playerId; // 유니티에서 보낸 ID
        const res = await axios({
          method: 'get',
          url: `https://cloud-save.services.api.unity.com/v1/data/projects/${PROJECT_A_ID}/players/${targetId}/items`,
          headers: { 'Authorization': `Bearer ${accessToken}` }
        });
        const goldItem = res.data.results.find(item => item.key.toLowerCase() === "gold");
        
        return {
          success: true,
          gold: goldItem ? Number(goldItem.value) : 0, // C# int 타입에 맞게 숫자로 변환
          message: ""
        };

      }
      catch (error) {
        // 3. 데이터가 아예 없어서 404 에러가 난 경우
        if (error.response && error.response.status === 404) {
          logger.info(`Player ${targetId}의 데이터가 전혀 존재하지 않아 초기화합니다.`);
          await initializeGold(targetId);
          return { success: true, gold: 0, error: "" };
        }else{
          // 그 외의 진짜 에러(권한 부족, 네트워크 등)
          logger.error(`골드 조회 중 실제 에러 발생: ${error.message}`);
          return { success: false, gold: 0, error: error.message };
        }
      }
    }

    // 중복 코드를 방지하기 위한 초기화 함수
    async function initializeGold(playerId) { 

      const saveResponse = await axios({
        method: 'post',
        url: `https://cloud-save.services.api.unity.com/v1/data/projects/${PROJECT_A_ID}/players/${playerId}/items`,
        headers: {
          'Authorization': `Bearer ${accessToken}`, // 이제 Bearer 방식을 사용합니다.
          'Content-Type': 'application/json'
        },
        data: { 
          "key": "gold", 
          "value": 0
        }
      });

      // await axios({
      //   method: 'post',
      //   url: `https://cloud-save.services.api.unity.com/v1/data/projects/${PROJECT_A_ID}/players/${playerId}/items`,
      //   headers: {
      //     'Authorization': `Bearer ${accessToken}`, // 이제 Bearer 방식을 사용합니다.
      //     'Content-Type': 'application/json'
      //   },
      //   data: { 
      //     "key": "gold", 
      //     "value": 1
      //   }
      // });
    }

    if (params.action === "SAVE_GOLD") {
      // 1. 파라미터 체크 (방어 코드)
      if (!params.playerId || params.gold === undefined) {
        return { success: false, message: "playerId 또는 gold 값이 누락되었습니다." };
      }

      const saveResponse = await axios({
        method: 'post',
        url: `https://cloud-save.services.api.unity.com/v1/data/projects/${PROJECT_A_ID}/players/${params.playerId}/items`,
        headers: {
          'Authorization': `Bearer ${accessToken}`, // 이제 Bearer 방식을 사용합니다.
          'Content-Type': 'application/json'
        },
        data: { 
          "key": "gold", 
          "value": params.gold
        }
      });

      // const saveResponse = await axios({
      //   method: 'get',
      //   url: `https://cloud-save.services.api.unity.com/v1/data/projects/${PROJECT_A_ID}/players/${params.playerId}/items`,
      //   headers: {
      //     'Authorization': `Bearer ${accessToken}`, // 이제 Bearer 방식을 사용합니다.
      //     'Content-Type': 'application/json'
      //   }
      // });

      return { 
        success: true, 
        message: `${params.playerId}에게 ${params.gold} 골드 저장 완료!`
      };
    }
  } catch (error) {
    logger.error("API 호출 실패: " + JSON.stringify(error.response?.data || error.message));
    return { success: false, error: error.response?.data?.title || error.message };
  }
}; 