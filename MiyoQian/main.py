# -*- coding: utf-8 -*-
"""米游签命令入口。

直接执行 `python main.py` 时启动 Web 服务；带参数时交给 CLI 处理。
"""

import sys

from miyouqian.cli import main


if __name__ == "__main__":
    if len(sys.argv) == 1:
        raise SystemExit(main(["serve"]))
    raise SystemExit(main())
