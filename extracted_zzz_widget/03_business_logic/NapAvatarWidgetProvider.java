package com.mihoyo.hyperion.biz.miniwidget.napavatar;

import Cy.G;
import G50.l;
import G50.m;
import android.appwidget.AppWidgetManager;
import android.content.Context;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import bq.g0;
import bq.r;
import com.mihoyo.commlib.utils.LogUtils;
import com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.NapSettingsActivity;
import com.mihoyo.hyperion.discuss.bean.PictureRankTabCate;
import com.mihoyo.hyperion.utils.ColdBootTimer;
import eq.a;
import hg.i;
import hg.j;
import hg.k;
import hg.o;
import hg.p;
import ig.c;
import java.util.HashMap;
import kotlin.jvm.internal.DefaultConstructorMarker;
import kotlin.jvm.internal.K;
import kotlin.jvm.internal.r0;
import pg.C0774d;
import uj.b;
import w5.D;
import x0.v;

@v(parameters = 1)
@r0({"SMAP\nNapAvatarWidgetProvider.kt\nKotlin\n*S Kotlin\n*F\n+ 1 NapAvatarWidgetProvider.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/NapAvatarWidgetProvider\n+ 2 WorkerUtil.kt\ncom/mihoyo/hyperion/biz/miniwidget/WorkerUtil\n*L\n1#1,181:1\n20#2,2:182\n20#2,2:184\n48#2,2:186\n52#2,4:188\n*S KotlinDebug\n*F\n+ 1 NapAvatarWidgetProvider.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/NapAvatarWidgetProvider\n*L\n61#1:182,2\n75#1:184,2\n159#1:186,2\n169#1:188,4\n*E\n"})
/* loaded from: X:\AI project\解包\tools\classes12.dex */
public final class NapAvatarWidgetProvider extends j {

    @l
    public static final a b = new a(null);
    public static final int c = 0;

    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class a {
        public /* synthetic */ a(DefaultConstructorMarker defaultConstructorMarker) {
            this();
        }

        public static /* synthetic */ void b(a aVar, String str, boolean z, String str2, int i, Object obj) {
            if ((i & 4) != 0) {
                str2 = "";
            }
            aVar.a(str, z, str2);
        }

        public final void a(@l String str, boolean z, @l String str2) {
            K.p(str, "btnName");
            K.p(str2, "btnId");
            g0 g0Var = new g0(str, k.NAP_AVATAR.getGameId(), "DeskModule", (Integer) null, (String) null, (HashMap) null, (HashMap) null, (a.a) null, str2, (String) null, (String) null, (String) null, 3832, (DefaultConstructorMarker) null);
            g0Var.f().put("api_version", "v3");
            if (z) {
                long timeAfterAppInitEnd = ColdBootTimer.INSTANCE.timeAfterAppInitEnd();
                LogUtils.INSTANCE.d("widget-nap-avatar", "launch app, boot time=" + timeAfterAppInitEnd);
                g0Var.f().put("is_coldboot", timeAfterAppInitEnd < 300 ? "1" : PictureRankTabCate.NO_CATE_ID);
            }
            r.F(g0Var, (Object) null, (String) null, 3, (Object) null);
        }

        public a() {
        }
    }

    /* JADX WARN: Can't fix incorrect switch cases order, some code will duplicate */
    private final boolean a(Context context, Intent intent) {
        String action = intent.getAction();
        if (action != null) {
            switch (action.hashCode()) {
                case -1978661782:
                    if (action.equals(C0774d.f)) {
                        a.b(b, "LocalRefresh", false, null, 4, null);
                        hg.l.a.a(context, G.k(k.NAP_AVATAR));
                        return true;
                    }
                    break;
                case -1001396547:
                    if (action.equals(C0774d.b)) {
                        a.b(b, "Mys", true, null, 4, null);
                        i.j(context);
                        return true;
                    }
                    break;
                case -978357578:
                    if (action.equals(C0774d.h)) {
                        a.b(b, "GameStart", true, null, 4, null);
                        String stringExtra = intent.getStringExtra("url");
                        if (stringExtra == null) {
                            return true;
                        }
                        Uri parse = Uri.parse("mihoyobbs://openGameByAppWidgetSchemeData?links=" + stringExtra);
                        Intent intent2 = new Intent("android.intent.action.VIEW", parse);
                        intent2.addCategory("android.intent.category.DEFAULT");
                        intent2.addCategory("android.intent.category.BROWSABLE");
                        intent2.setData(parse);
                        intent2.setPackage(context.getPackageName());
                        i.k(context, intent2);
                        return true;
                    }
                    break;
                case -978178947:
                    if (action.equals(C0774d.i)) {
                        a.b(b, "Mys", true, null, 4, null);
                        String stringExtra2 = intent.getStringExtra("url");
                        if (stringExtra2 == null) {
                            return true;
                        }
                        b.a(context, stringExtra2);
                        return true;
                    }
                    break;
                case -978135370:
                    if (action.equals(C0774d.e)) {
                        a.b(b, "DailyNote", true, null, 4, null);
                        b(context, intent);
                        return true;
                    }
                    break;
                case -977992575:
                    if (action.equals(C0774d.d)) {
                        a.b(b, "Sign", true, null, 4, null);
                        b(context, intent);
                        return true;
                    }
                    break;
                case 714534421:
                    if (action.equals(C0774d.c)) {
                        a.b(b, "GameRecord", true, null, 4, null);
                        b(context, intent);
                        return true;
                    }
                    break;
                case 1579074796:
                    if (action.equals(C0774d.g)) {
                        a.b(b, "Setting", true, null, 4, null);
                        NapSettingsActivity.g.a(context);
                        return true;
                    }
                    break;
            }
        }
        return false;
    }

    private final void b(Context context, Intent intent) {
        String stringExtra = intent.getStringExtra("url");
        if (stringExtra == null) {
            stringExtra = "";
        }
        if (stringExtra.length() > 0) {
            i.l(context, stringExtra);
        } else {
            i.j(context);
        }
    }

    @Override // android.appwidget.AppWidgetProvider
    public void onAppWidgetOptionsChanged(@l Context context, @m AppWidgetManager appWidgetManager, int i, @m Bundle bundle) {
        K.p(context, "context");
        super.onAppWidgetOptionsChanged(context, appWidgetManager, i, bundle);
        if (!o.a.e(k.NAP_AVATAR)) {
            NapAvatarWidgetWorker.k.c(context, appWidgetManager, i);
        }
        p.a.c(context, NapAvatarWidgetWorker.class);
    }

    @Override // android.appwidget.AppWidgetProvider
    public void onDisabled(@l Context context) {
        K.p(context, "context");
        super.onDisabled(context);
        LogUtils logUtils = LogUtils.INSTANCE;
        logUtils.i("widget-nap-avatar", "onDisabled");
        a.b(b, "Delete", false, null, 4, null);
        c cVar = c.a;
        k kVar = k.NAP_AVATAR;
        cVar.b(kVar);
        o.a.b(kVar);
        p pVar = p.a;
        String simpleName = NapAvatarWidgetWorker.class.getSimpleName();
        logUtils.i(hg.l.b, "cancel: " + simpleName);
        D.p(context).g(simpleName);
    }

    @Override // android.appwidget.AppWidgetProvider
    public void onEnabled(@l Context context) {
        K.p(context, "context");
        super.onEnabled(context);
        LogUtils.INSTANCE.i("widget-nap-avatar", "onEnabled");
        a.b(b, "Start", false, null, 4, null);
    }

    @Override // hg.j, android.appwidget.AppWidgetProvider, android.content.BroadcastReceiver
    public void onReceive(@l Context context, @l Intent intent) {
        K.p(context, "context");
        K.p(intent, "intent");
        super.onReceive(context, intent);
        LogUtils.INSTANCE.d("widget-nap-avatar", "onReceive: " + intent.getAction() + ", url=" + intent.getStringExtra("url"));
        if (a(context, intent)) {
            p.a.c(context, NapAvatarWidgetWorker.class);
        }
    }

    @Override // android.appwidget.AppWidgetProvider
    public void onUpdate(@m Context context, @m AppWidgetManager appWidgetManager, @m int[] iArr) {
        super.onUpdate(context, appWidgetManager, iArr);
        if (context == null || appWidgetManager == null || iArr == null) {
            return;
        }
        LogUtils.INSTANCE.i("widget-nap-avatar", "onUpdate");
        p.a.e(context, NapAvatarWidgetWorker.class);
        hg.l.a.c(context, k.NAP_AVATAR);
    }
}
