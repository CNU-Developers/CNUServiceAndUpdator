using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;

namespace CNU.Service
{
    static class Program
    {
        // 윈도우 서비스는 Main함수를 안씁니다. 얜 그냥 없어도됨ㅠ
        // 얜 실행파일을 강제로 실행시키려할경우(ex F5키디버깅, 등) 추가된 서비스를 테스트 하기 위함.
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[] 
			{ 
				new CNUService()
			};
            ServiceBase.Run(ServicesToRun);
        }
    }
}
