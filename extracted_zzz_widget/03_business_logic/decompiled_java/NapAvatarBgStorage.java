package rg;

import Ce.F0;
import G50.l;
import G50.m;
import android.content.SharedPreferences;
import com.mihoyo.commlib.utils.SPUtils;
import com.mihoyo.hyperion.biz.miniwidget.napavatar.AvatarBackground;
import kotlin.jvm.internal.K;
import pg.C0772b;
import pg.InterfaceC0771a;
import x0.v;
import yA.P;
import ye.e;

@v(parameters = 1)
/* renamed from: rg.a, reason: case insensitive filesystem */
/* loaded from: X:\AI project\解包\tools\decompiled_res\resources\classes12.dex */
public final class C0792a {

    @l
    public static final C0792a a = new C0792a();

    @l
    public static final String b = "key_nap_avatar_bg";
    public static final int c = 0;

    @l
    public final InterfaceC0771a a() {
        InterfaceC0771a b2;
        String string = SPUtils.INSTANCE.getInstance(SPUtils.a.SP_WIDGET_NAP_AVATAR_BG).getString(b, "");
        String str = string != null ? string : "";
        if (P.O3(str)) {
            return b();
        }
        try {
            b2 = (InterfaceC0771a) e.b().fromJson(str, AvatarBackground.a.class);
        } catch (Exception unused) {
            b2 = b();
        }
        K.m(b2);
        return b2;
    }

    @l
    public final C0772b b() {
        return new C0772b(0, null, null, null, 15, null);
    }

    @m
    public final AvatarBackground.a c() {
        String string = SPUtils.INSTANCE.getInstance(SPUtils.a.SP_WIDGET_NAP_AVATAR_BG).getString(b, "");
        String str = string != null ? string : "";
        if (!P.O3(str)) {
            try {
            } catch (Exception unused) {
                return null;
            }
        }
        return (AvatarBackground.a) e.b().fromJson(str, AvatarBackground.a.class);
    }

    public final void d(@l AvatarBackground.a aVar) {
        K.p(aVar, "info");
        String json = e.b().toJson(aVar);
        SharedPreferences sPUtils = SPUtils.INSTANCE.getInstance(SPUtils.a.SP_WIDGET_NAP_AVATAR_BG);
        K.m(json);
        F0.B(sPUtils, b, json);
    }
}
