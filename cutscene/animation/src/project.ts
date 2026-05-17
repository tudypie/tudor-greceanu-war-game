import {makeProject} from '@motion-canvas/core';

import intro from './scenes/intro?scene';
import cutscene1 from './scenes/cutscene1_briefing?scene';
import cutscene2 from './scenes/cutscene2_intoarcerea?scene';
import cutscene3 from './scenes/cutscene3_dupa_razboi?scene';

export default makeProject({
  scenes: [intro, cutscene1, cutscene2, cutscene3],
});
