package com.mihoyo.hyperion.biz.miniwidget.nap;

import Cy.G;
import G50.m;
import android.appwidget.AppWidgetManager;
import android.content.Context;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import bq.g0;
import bq.r;
import com.mihoyo.commlib.utils.LogUtils;
import com.mihoyo.hyperion.discuss.bean.PictureRankTabCate;
import com.mihoyo.hyperion.utils.ColdBootTimer;
import eq.a;
import hg.i;
import hg.j;
import hg.k;
import hg.l;
import hg.o;
import hg.p;
import ig.c;
import java.util.HashMap;
import kotlin.jvm.internal.DefaultConstructorMarker;
import kotlin.jvm.internal.K;
import kotlin.jvm.internal.r0;
import og.C0731a;
import uj.b;
import w5.D;
import x0.v;

@v(parameters = 1)
@r0({"SMAP\nNapWidgetProvider.kt\nKotlin\n*S Kotlin\n*F\n+ 1 NapWidgetProvider.kt\ncom/mihoyo/hyperion/biz/miniwidget/nap/NapWidgetProvider\n+ 2 WorkerUtil.kt\ncom/mihoyo/hyperion/biz/miniwidget/WorkerUtil\n*L\n1#1,171:1\n20#2,2:172\n20#2,2:174\n48#2,2:176\n52#2,4:178\n*S KotlinDebug\n*F\n+ 1 NapWidgetProvider.kt\ncom/mihoyo/hyperion/biz/miniwidget/nap/NapWidgetProvider\n*L\n39#1:172,2\n53#1:174,2\n131#1:176,2\n141#1:178,4\n*E\n"})
/* loaded from: X:\AI project\解包\tools\classes12.dex */
public final class NapWidgetProvider extends j {
    public static final int b = 0;

    /* JADX WARN: Can't fix incorrect switch cases order, some code will duplicate */
    private final boolean a(Context context, Intent intent) {
        String action = intent.getAction();
        if (action != null) {
            switch (action.hashCode()) {
                case -1719089586:
                    if (action.equals(C0731a.c)) {
                        c("GameRecord", true);
                        b(context, intent);
                        return true;
                    }
                    break;
                case -1700732637:
                    if (action.equals(C0731a.f)) {
                        c("Refresh", true);
                        l.a.a(context, G.k(k.NAP));
                        return true;
                    }
                    break;
                case -975527644:
                    if (action.equals(C0731a.b)) {
                        c("Mys", true);
                        i.j(context);
                        return true;
                    }
                    break;
                case -176421585:
                    if (action.equals(C0731a.g)) {
                        c("GameStart", true);
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
                case -176242954:
                    if (action.equals(C0731a.h)) {
                        c("Mys", true);
                        String stringExtra2 = intent.getStringExtra("url");
                        if (stringExtra2 == null) {
                            return true;
                        }
                        b.a(context, stringExtra2);
                        return true;
                    }
                    break;
                case -176199377:
                    if (action.equals(C0731a.e)) {
                        c("DailyNote", true);
                        b(context, intent);
                        return true;
                    }
                    break;
                case -176056582:
                    if (action.equals(C0731a.d)) {
                        c("Sign", true);
                        b(context, intent);
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

    public final void c(String str, boolean z) {
        g0 g0Var = new g0(str, k.NAP.getGameId(), "DeskModule", (Integer) null, (String) null, (HashMap) null, (HashMap) null, (a.a) null, (String) null, (String) null, (String) null, (String) null, 4088, (DefaultConstructorMarker) null);
        g0Var.f().put("api_version", "v4");
        if (z) {
            long timeAfterAppInitEnd = ColdBootTimer.INSTANCE.timeAfterAppInitEnd();
            LogUtils.INSTANCE.d("widget-nap", "launch app, boot time=" + timeAfterAppInitEnd);
            g0Var.f().put("is_coldboot", timeAfterAppInitEnd < 300 ? "1" : PictureRankTabCate.NO_CATE_ID);
        }
        r.F(g0Var, (Object) null, (String) null, 3, (Object) null);
    }

    @Override // android.appwidget.AppWidgetProvider
    public void onAppWidgetOptionsChanged(@G50.l Context context, @m AppWidgetManager appWidgetManager, int i, @m Bundle bundle) {
        K.p(context, "context");
        super.onAppWidgetOptionsChanged(context, appWidgetManager, i, bundle);
        if (!o.a.e(k.NAP)) {
            NapWidgetWorker.k.b(context, appWidgetManager, i);
        }
        p.a.c(context, NapWidgetWorker.class);
    }

    @Override // android.appwidget.AppWidgetProvider
    public void onDisabled(@G50.l Context context) {
        K.p(context, "context");
        super.onDisabled(context);
        LogUtils logUtils = LogUtils.INSTANCE;
        logUtils.i("widget-nap", "onDisabled");
        c("Delete", false);
        c cVar = c.a;
        k kVar = k.NAP;
        cVar.b(kVar);
        o.a.b(kVar);
        p pVar = p.a;
        String simpleName = NapWidgetWorker.class.getSimpleName();
        logUtils.i(l.b, "cancel: " + simpleName);
        D.p(context).g(simpleName);
    }

    @Override // android.appwidget.AppWidgetProvider
    public void onEnabled(@G50.l Context context) {
        K.p(context, "context");
        super.onEnabled(context);
        LogUtils.INSTANCE.i("widget-nap", "onEnabled");
        c("Start", false);
    }

    @Override // hg.j, android.appwidget.AppWidgetProvider, android.content.BroadcastReceiver
    public void onReceive(@G50.l Context context, @G50.l Intent intent) {
        K.p(context, "context");
        K.p(intent, "intent");
        super.onReceive(context, intent);
        LogUtils.INSTANCE.d("widget-nap", "onReceive: " + intent.getAction() + ", url=" + intent.getStringExtra("url"));
        if (a(context, intent)) {
            p.a.c(context, NapWidgetWorker.class);
        }
    }

    @Override // android.appwidget.AppWidgetProvider
    public void onUpdate(@m Context context, @m AppWidgetManager appWidgetManager, @m int[] iArr) {
        super.onUpdate(context, appWidgetManager, iArr);
        if (context == null || appWidgetManager == null || iArr == null) {
            return;
        }
        LogUtils.INSTANCE.i("widget-nap", "onUpdate");
        p.a.e(context, NapWidgetWorker.class);
        l.a.c(context, k.NAP);
    }
}
