package com.mihoyo.hyperion.biz.miniwidget.nap;

import Ce.P;
import Cy.A;
import Cy.H;
import G50.l;
import Lx.B;
import Ly.f;
import Oy.b;
import Tx.g;
import android.annotation.SuppressLint;
import android.app.PendingIntent;
import android.appwidget.AppWidgetManager;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.graphics.Color;
import android.os.Bundle;
import android.os.SystemClock;
import android.widget.RemoteViews;
import androidx.work.WorkerParameters;
import com.mihoyo.hyperion.biz.miniwidget.BaseWidgetWorker;
import com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetData;
import com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetWorker;
import com.mihoyo.hyperion.biz.miniwidget.nap.a;
import com.mihoyo.hyperion.manager.AppConfigKt;
import com.mihoyo.hyperion.net.model.bean.CommonResponseInfo;
import d2.d;
import hg.h;
import hg.k;
import hg.m;
import hg.o;
import i.D;
import ig.C0595a;
import ig.c;
import kotlin.jvm.functions.Function1;
import kotlin.jvm.internal.DefaultConstructorMarker;
import kotlin.jvm.internal.K;
import kotlin.jvm.internal.r0;
import og.C0731a;
import og.C0732b;
import x0.v;
import ye.e;
import zy.N0;

@v(parameters = 1)
@r0({"SMAP\nNapWidgetWorker.kt\nKotlin\n*S Kotlin\n*F\n+ 1 NapWidgetWorker.kt\ncom/mihoyo/hyperion/biz/miniwidget/nap/NapWidgetWorker\n+ 2 Context.kt\nandroidx/core/content/ContextKt\n+ 3 RetrofitClient.kt\ncom/mihoyo/hyperion/net/RetrofitClient\n+ 4 _Collections.kt\nkotlin/collections/CollectionsKt___CollectionsKt\n*L\n1#1,374:1\n31#2:375\n161#3:376\n1878#4,3:377\n*S KotlinDebug\n*F\n+ 1 NapWidgetWorker.kt\ncom/mihoyo/hyperion/biz/miniwidget/nap/NapWidgetWorker\n*L\n74#1:375\n101#1:376\n196#1:377,3\n*E\n"})
/* loaded from: X:\AI project\解包\tools\classes12.dex */
public final class NapWidgetWorker extends BaseWidgetWorker {
    public static final int l = 0;
    public static long m;

    @l
    public static final a k = new a(null);
    public static final int n = m.C0090m.Z2;

    @r0({"SMAP\nNapWidgetWorker.kt\nKotlin\n*S Kotlin\n*F\n+ 1 NapWidgetWorker.kt\ncom/mihoyo/hyperion/biz/miniwidget/nap/NapWidgetWorker$Companion\n+ 2 fake.kt\nkotlin/jvm/internal/FakeKt\n+ 3 Extensions.kt\ncom/mihoyo/hyperion/biz/miniwidget/ExtensionsKt\n*L\n1#1,374:1\n1#2:375\n79#3,11:376\n*S KotlinDebug\n*F\n+ 1 NapWidgetWorker.kt\ncom/mihoyo/hyperion/biz/miniwidget/nap/NapWidgetWorker$Companion\n*L\n65#1:376,11\n*E\n"})
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class a {
        public /* synthetic */ a(DefaultConstructorMarker defaultConstructorMarker) {
            this();
        }

        public static /* synthetic */ void d(a aVar, RemoteViews remoteViews, Context context, int i, String str, String str2, int i2, Object obj) {
            if ((i2 & 8) != 0) {
                str2 = "";
            }
            aVar.c(remoteViews, context, i, str, str2);
        }

        public final void b(@l Context context, @G50.m AppWidgetManager appWidgetManager, int i) {
            K.p(context, "context");
            if (appWidgetManager == null) {
                appWidgetManager = AppWidgetManager.getInstance(context);
            }
            RemoteViews remoteViews = new RemoteViews(context.getPackageName(), NapWidgetWorker.n);
            a aVar = NapWidgetWorker.k;
            d(aVar, remoteViews, context, m.j.Fl, C0731a.b, null, 8, null);
            d(aVar, remoteViews, context, m.j.R3, C0731a.f, null, 8, null);
            d(aVar, remoteViews, context, m.j.a8, C0731a.b, null, 8, null);
            appWidgetManager.updateAppWidget(i, remoteViews);
        }

        public final void c(RemoteViews remoteViews, Context context, int i, String str, String str2) {
            Bundle bundle;
            if (str2.length() > 0) {
                bundle = new Bundle();
                bundle.putString("url", str2);
            } else {
                bundle = null;
            }
            Intent intent = new Intent(context, (Class<?>) NapWidgetProvider.class);
            intent.setAction(str);
            if (bundle != null) {
                intent.putExtras(bundle);
            }
            remoteViews.setOnClickPendingIntent(i, PendingIntent.getBroadcast(context, 0, intent, 201326592));
        }

        public a() {
        }
    }

    /* JADX WARN: 'super' call moved to the top of the method (can break code semantics) */
    public NapWidgetWorker(@l Context context, @l WorkerParameters workerParameters) {
        super(context, workerParameters);
        K.p(context, "context");
        K.p(workerParameters, "workerParameters");
    }

    public static /* synthetic */ void S(NapWidgetWorker napWidgetWorker, RemoteViews remoteViews, Context context, int i, int i2, int i3, NapWidgetData.b bVar, Integer num, int i4, Object obj) {
        napWidgetWorker.R(remoteViews, context, i, i2, i3, bVar, (i4 & 32) != 0 ? null : num);
    }

    private final RemoteViews T(Context context, int i) {
        String str;
        String str2;
        if (i == -999) {
            C0595a.a.b("widget-nap", "showErrorUi: network error");
            str = "数据加载失败";
            str2 = "点击刷新或进入App查看";
        } else if (i == -100) {
            C0595a.a.b("widget-nap", "showErrorUi: not login");
            str = "绳匠还没有登录";
            str2 = "点击进入米游社重新登录";
        } else if (i != 10110) {
            str = "数据加载错误~";
            str2 = "";
        } else {
            C0595a.a.b("widget-nap", "showErrorUi: no account");
            str = "没有查询到角色";
            str2 = "点击进入米游社查看战绩";
        }
        RemoteViews remoteViews = new RemoteViews(context.getPackageName(), n);
        remoteViews.setViewVisibility(m.j.pq, 0);
        remoteViews.setViewVisibility(m.j.Wq, 8);
        remoteViews.setViewVisibility(m.j.yq, 8);
        remoteViews.setViewVisibility(m.j.a8, 0);
        remoteViews.setViewVisibility(m.j.R3, 0);
        remoteViews.setTextViewText(m.j.vq, str);
        remoteViews.setTextViewText(m.j.um, str2);
        remoteViews.setViewVisibility(m.j.Z7, str2.length() <= 0 ? 8 : 0);
        a aVar = k;
        a.d(aVar, remoteViews, context, m.j.Fl, C0731a.b, null, 8, null);
        a.d(aVar, remoteViews, context, m.j.a8, C0731a.b, null, 8, null);
        a.d(aVar, remoteViews, context, m.j.R3, C0731a.f, null, 8, null);
        return remoteViews;
    }

    public static final NapWidgetData U(CommonResponseInfo commonResponseInfo) {
        K.p(commonResponseInfo, "it");
        return (NapWidgetData) commonResponseInfo.getData();
    }

    public static final NapWidgetData V(Function1 function1, Object obj) {
        K.p(obj, "p0");
        return (NapWidgetData) function1.invoke(obj);
    }

    public static final N0 W(long j, String str, AppWidgetManager appWidgetManager, int[] iArr, NapWidgetWorker napWidgetWorker, Context context, NapWidgetData napWidgetData) {
        long elapsedRealtime = SystemClock.elapsedRealtime() - j;
        C0595a c0595a = C0595a.a;
        c0595a.b(str, "request success, cost=" + elapsedRealtime + "ms");
        K.m(napWidgetData);
        appWidgetManager.updateAppWidget(iArr, napWidgetWorker.a0(context, napWidgetData, iArr));
        c0595a.b(str, "ui update success, totalCost=" + (SystemClock.elapsedRealtime() - j) + "ms");
        c cVar = c.a;
        k kVar = k.NAP;
        cVar.f(kVar, true, "");
        o.a.f(kVar);
        return N0.a;
    }

    /* JADX INFO: Access modifiers changed from: private */
    public static final void X(Function1 function1, Object obj) {
        function1.invoke(obj);
    }

    public static final N0 Y(long j, AppWidgetManager appWidgetManager, int[] iArr, NapWidgetWorker napWidgetWorker, Context context, String str, Throwable th2) {
        long elapsedRealtime = SystemClock.elapsedRealtime() - j;
        boolean z = th2 instanceof ne.a;
        String str2 = "";
        if (z) {
            ne.a aVar = (ne.a) th2;
            if (H.O(new Integer[]{-100, Integer.valueOf(h.d)}).contains(Integer.valueOf(aVar.a()))) {
                appWidgetManager.updateAppWidget(iArr, napWidgetWorker.T(context, aVar.a()));
                c.a.f(k.NAP, true, "");
                return N0.a;
            }
        }
        if (z) {
            ne.a aVar2 = (ne.a) th2;
            str2 = "network error: " + aVar2.a() + " " + aVar2.getMessage();
        } else {
            String message = th2.getMessage();
            if (message != null) {
                str2 = message;
            }
        }
        C0595a c0595a = C0595a.a;
        c0595a.b(str, "failed: [" + th2.getClass().getSimpleName() + "] " + str2 + ", cost=" + elapsedRealtime + "ms");
        if (!AppConfigKt.getAppConfig().isWidgetFixEnable() || o.a.e(k.NAP)) {
            c0595a.b(str, "keep current ui because successful data exists");
        } else {
            try {
                appWidgetManager.updateAppWidget(iArr, napWidgetWorker.T(context, h.e));
            } catch (Throwable unused) {
            }
        }
        c.a.f(k.NAP, false, str2);
        return N0.a;
    }

    /* JADX INFO: Access modifiers changed from: private */
    public static final void Z(Function1 function1, Object obj) {
        function1.invoke(obj);
    }

    @Override // com.mihoyo.hyperion.biz.miniwidget.BaseWidgetWorker
    @G50.m
    @SuppressLint({"CheckResult"})
    public Object I(@l final Context context, @l f<? super Boolean> fVar) {
        final long elapsedRealtime = SystemClock.elapsedRealtime();
        C0595a c0595a = C0595a.a;
        final String str = "widget-nap";
        c0595a.b("widget-nap", "update start");
        final AppWidgetManager appWidgetManager = (AppWidgetManager) d.getSystemService(context, AppWidgetManager.class);
        if (appWidgetManager == null) {
            c0595a.b("widget-nap", "update fail: appWidgetManager is null");
            return b.a(false);
        }
        final int[] appWidgetIds = appWidgetManager.getAppWidgetIds(new ComponentName(context, (Class<?>) NapWidgetProvider.class));
        if (appWidgetIds == null) {
            c0595a.b("widget-nap", "update fail: widgetIds is null");
            return b.a(false);
        }
        if (appWidgetIds.length == 0) {
            c0595a.b("widget-nap", "update fail: widgetIds is empty");
            return b.a(false);
        }
        c0595a.b("widget-nap", "widgetIds=" + A.Ry(appWidgetIds));
        long currentTimeMillis = System.currentTimeMillis();
        if (currentTimeMillis - m < BaseWidgetWorker.a.d) {
            c0595a.b("widget-nap", "update fail: request too frequently");
            return b.a(false);
        }
        m = currentTimeMillis;
        c0595a.b("widget-nap", "startNetworkRequest");
        B S4 = a.C0039a.a((com.mihoyo.hyperion.biz.miniwidget.nap.a) Ym.A.a.n(com.mihoyo.hyperion.biz.miniwidget.nap.a.class), null, 1, null).S4(new BaseWidgetWorker.a());
        final Function1 function1 = new Function1() { // from class: og.c
            public final Object invoke(Object obj) {
                NapWidgetData U;
                U = NapWidgetWorker.U((CommonResponseInfo) obj);
                return U;
            }
        };
        B A3 = S4.A3(new Tx.o() { // from class: og.d
            public final Object apply(Object obj) {
                NapWidgetData V;
                V = NapWidgetWorker.V(function1, obj);
                return V;
            }
        });
        K.o(A3, "map(...)");
        B E = P.E(A3);
        final Function1 function12 = new Function1() { // from class: og.e
            public final Object invoke(Object obj) {
                N0 W;
                W = NapWidgetWorker.W(elapsedRealtime, str, appWidgetManager, appWidgetIds, this, context, (NapWidgetData) obj);
                return W;
            }
        };
        g gVar = new g() { // from class: og.f
            public final void accept(Object obj) {
                NapWidgetWorker.X(function12, obj);
            }
        };
        final Function1 function13 = new Function1() { // from class: og.g
            public final Object invoke(Object obj) {
                N0 Y;
                Y = NapWidgetWorker.Y(elapsedRealtime, appWidgetManager, appWidgetIds, this, context, str, (Throwable) obj);
                return Y;
            }
        };
        E.F5(gVar, new g() { // from class: og.h
            public final void accept(Object obj) {
                NapWidgetWorker.Z(function13, obj);
            }
        });
        return b.a(true);
    }

    public final RemoteViews Q(Context context, NapWidgetData napWidgetData) {
        RemoteViews remoteViews = new RemoteViews(context.getPackageName(), m.C0090m.V7);
        remoteViews.setTextViewText(m.j.g6, napWidgetData.getVitality().getCurrent() + "/" + napWidgetData.getVitality().getMax());
        if (napWidgetData.getVitality().getCurrent() < napWidgetData.getVitality().getMax()) {
            remoteViews.setTextColor(m.j.g6, context.getColor(m.f.Bm));
        } else {
            remoteViews.setTextColor(m.j.g6, context.getColor(m.f.X6));
        }
        return remoteViews;
    }

    public final void R(RemoteViews remoteViews, Context context, @D int i, @D int i2, @D int i3, NapWidgetData.b bVar, @D Integer num) {
        remoteViews.setViewVisibility(i, 0);
        remoteViews.setTextViewText(i2, bVar.g());
        try {
            remoteViews.setTextColor(i2, Color.parseColor(Hh.k.l + bVar.h()));
        } catch (Exception unused) {
            remoteViews.setTextColor(i2, Pt.D.a(context, m.f.Gm));
        }
        remoteViews.setTextViewText(i3, bVar.i());
        try {
            remoteViews.setTextColor(i3, Color.parseColor(Hh.k.l + bVar.j()));
        } catch (Exception unused2) {
            remoteViews.setTextColor(i3, Pt.D.a(context, m.f.X6));
        }
        if (num != null) {
            remoteViews.setViewVisibility(num.intValue(), 0);
        }
    }

    public final RemoteViews a0(Context context, NapWidgetData napWidgetData, int[] iArr) {
        RemoteViews remoteViews = new RemoteViews(context.getPackageName(), n);
        remoteViews.setViewVisibility(m.j.a8, 8);
        int i = 0;
        remoteViews.setViewVisibility(m.j.R3, 0);
        remoteViews.setViewVisibility(m.j.Wq, 0);
        remoteViews.setViewVisibility(m.j.yq, 0);
        if (napWidgetData.getHasSigned()) {
            remoteViews.setTextViewText(m.j.Wq, "前往社区");
        } else {
            remoteViews.setTextViewText(m.j.Wq, "尚未签到");
        }
        remoteViews.setTextViewText(m.j.tq, String.valueOf(napWidgetData.getEnergy().getProgress().getCurrent()));
        remoteViews.setTextViewText(m.j.uq, "/" + napWidgetData.getEnergy().getProgress().getMax());
        if (napWidgetData.getEnergy().getProgress().getCurrent() < napWidgetData.getEnergy().getProgress().getMax()) {
            remoteViews.setTextViewText(m.j.lc, C0732b.a.a(napWidgetData.getEnergy().getRestore()));
        } else {
            remoteViews.setTextViewText(m.j.lc, "已全部恢复");
        }
        remoteViews.removeAllViews(m.j.ue);
        remoteViews.addView(m.j.ue, Q(context, napWidgetData));
        remoteViews.setViewVisibility(m.j.t9, 4);
        remoteViews.setViewVisibility(m.j.sm, 4);
        remoteViews.setViewVisibility(m.j.ap, 4);
        remoteViews.setViewVisibility(m.j.U9, 4);
        remoteViews.setViewVisibility(m.j.ye, 4);
        remoteViews.setViewVisibility(m.j.ze, 4);
        remoteViews.setViewVisibility(m.j.Ae, 4);
        for (Object obj : napWidgetData.getNoteList()) {
            int i2 = i + 1;
            if (i < 0) {
                H.Z();
            }
            NapWidgetData.b bVar = (NapWidgetData.b) obj;
            if (i == 0) {
                S(this, remoteViews, context, m.j.t9, m.j.w9, m.j.x9, bVar, null, 32, null);
            } else if (i == 1) {
                R(remoteViews, context, m.j.sm, m.j.vm, m.j.wm, bVar, Integer.valueOf(m.j.ye));
            } else if (i == 2) {
                R(remoteViews, context, m.j.ap, m.j.cp, m.j.dp, bVar, Integer.valueOf(m.j.ze));
            } else if (i == 3) {
                R(remoteViews, context, m.j.U9, m.j.W9, m.j.X9, bVar, Integer.valueOf(m.j.Ae));
            }
            i = i2;
        }
        if (napWidgetData.getHasSigned()) {
            k.c(remoteViews, context, m.j.Wq, C0731a.h, napWidgetData.getMainHomeUrl());
        } else {
            k.c(remoteViews, context, m.j.Wq, C0731a.d, napWidgetData.getSignUrl());
        }
        a aVar = k;
        a.d(aVar, remoteViews, context, m.j.R3, C0731a.f, null, 8, null);
        int i3 = m.j.yq;
        String json = e.b().toJson(napWidgetData.getPlayLinks());
        K.o(json, "toJson(...)");
        aVar.c(remoteViews, context, i3, C0731a.g, json);
        aVar.c(remoteViews, context, m.j.v5, C0731a.e, napWidgetData.getNoteUrl());
        aVar.c(remoteViews, context, m.j.w5, C0731a.e, napWidgetData.getNoteUrl());
        return remoteViews;
    }
}
