package com.mihoyo.hyperion.biz.miniwidget.napavatar;

import G50.l;
import G50.m;
import Ly.f;
import com.mihoyo.hyperion.net.model.bean.CommonResponseInfo;
import d60.k;
import d60.t;

/* loaded from: X:\AI project\解包\tools\classes12.dex */
public interface a {

    /* renamed from: com.mihoyo.hyperion.biz.miniwidget.napavatar.a$a, reason: collision with other inner class name */
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class C0041a {
        public static /* synthetic */ Object a(a aVar, String str, f fVar, int i, Object obj) {
            if (obj != null) {
                throw new UnsupportedOperationException("Super calls with default arguments not supported in this target, function: getWidgetData");
            }
            if ((i & 1) != 0) {
                str = "WV_V2";
            }
            return aVar.a(str, fVar);
        }
    }

    @m
    @k({"urlname:API_RECORD"})
    @d60.f("event/game_record_zzz/api/zzz/widget")
    Object a(@t("version") @l String str, @l f<? super CommonResponseInfo<NapWidgetData>> fVar);

    @m
    @k({"urlname:API_RECORD"})
    @d60.f("event/game_record_zzz/api/zzz/get_widget_avatar_background")
    Object b(@l f<? super CommonResponseInfo<AvatarBackground>> fVar);
}
