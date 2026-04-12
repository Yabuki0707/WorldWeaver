#TODO 点集系统需要一次重构。
第一是命名不规范，PointListShape内部却是Array数组，这并不优雅。
第二是四者可提取共同点即可输出span
第三是这MutablePointListShape却没有是否为脏属性，导致需要经常性的重算。

因此该系统需要重构，但目前仍能够使用。