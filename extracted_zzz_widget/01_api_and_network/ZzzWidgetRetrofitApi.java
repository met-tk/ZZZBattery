package com.mihoyo.hyperion.biz.miniwidget.nap;

import G50.l;
import Lx.B;
import com.mihoyo.hyperion.net.model.bean.CommonResponseInfo;
import d60.f;
import d60.k;
import d60.t;

/* loaded from: X:\AI project\解包\tools\classes12.dex */
public interface a {

    /* renamed from: com.mihoyo.hyperion.biz.miniwidget.nap.a$a, reason: collision with other inner class name */
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class C0039a {
        public static /* synthetic */ B a(a aVar, String str, int i, Object obj) {
            if (obj != null) {
                throw new UnsupportedOperationException("Super calls with default arguments not supported in this target, function: getWidgetData");
            }
            if ((i & 1) != 0) {
                str = "WV_Default";
            }
            return aVar.a(str);
        }
    }

    @k({"urlname:API_RECORD"})
    @l
    @f("event/game_record_zzz/api/zzz/widget")
    B<CommonResponseInfo<NapWidgetData>> a(@t("version") @l String str);
}
